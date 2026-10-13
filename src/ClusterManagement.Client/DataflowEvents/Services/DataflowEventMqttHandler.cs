using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ClusterManagement.Client.Management.Extensions;
using ClusterManagement.Public.DataflowEvents;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using Sdk.Messaging;

namespace ClusterManagement.Client.DataflowEvents.Services;

internal sealed partial class DataflowEventMqttHandler(IDataflowEventMqttClientProvider mqttProvider, ILogger<DataflowEventMqttHandler> logger) : IDataflowEventMqttHandler, IAsyncDisposable
{
    private const int ReconnectIntervalSeconds = 5;
    private static readonly TimeSpan s_reconnectInterval = TimeSpan.FromSeconds(ReconnectIntervalSeconds);

    private static readonly byte[] s_validityOne = "1"u8.ToArray();
    private static readonly byte[] s_engineCycleOne = "1"u8.ToArray();

    private PeriodicTimer? _reconnectTimer;
    private Task? _reconnectLoopTask;
    private CancellationTokenSource? _reconnectCancellation;

    private readonly ILogger<DataflowEventMqttHandler> _logger = logger;
    private readonly MqttQualityOfServiceLevel _subscriptionServiceLevel = MqttQualityOfServiceLevel.AtMostOnce;
    private readonly MqttQualityOfServiceLevel _writeServiceLevel = MqttQualityOfServiceLevel.AtMostOnce;
    private readonly ConcurrentDictionary<string, IDataflowEvent> _activeTopics = new();

    // Requests and reconnects go out one at a time, so a request for a topic never overtakes an earlier one for it.
    private readonly SemaphoreSlim _requestLock = new(1, 1);

    private IMqttClient? _client;
    private MqttClientOptions? _options;

    public event Func<DataflowEventArgs, Task>? EventReceivedAsync;

    public async Task Disconnect()
    {
        if (_client is null)
            return;

        await StopReconnectLoop();

        _client.ApplicationMessageReceivedAsync -= HandleApplicationMessageReceived;

        if (_client.IsConnected)
            await _client.DisconnectAsync();

        _client.Dispose();
        _client = null;
    }

    public async ValueTask DisposeAsync()
    {
        await Disconnect();
        _requestLock.Dispose();
    }

    private async Task<bool> EnsureConnected(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_client is null)
            {
                _client = mqttProvider.CreateMqttClient();
                _client.ApplicationMessageReceivedAsync += HandleApplicationMessageReceived;
            }

            if (_client.IsConnected)
                return true;

            _options = await mqttProvider.CreateSystemMqttClientOptions();
            var result = await _client.ConnectAsync(_options, cancellationToken);
            if (result.ResultCode == MqttClientConnectResultCode.Success)
            {
                // A connection that dropped comes back without the topics it had.
                await ResubscribeActiveTopics(cancellationToken);
                StartReconnectLoop();
                return true;
            }

            LogCouldNotConnectToDataflowEvents(_logger, result.ResultCode);
        }
        catch (Exception exception)
        {
            LogCouldNotConnectToDataflowEventsError(_logger, exception);
        }
        return false;
    }

    private async Task HandleApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs args)
    {
        if (EventReceivedAsync is null)
            return;

        if (args.ApplicationMessage.Payload.IsEmpty)
            return;

        if (!Guid.TryParse(args.ApplicationMessage.Topic, out var eventId))
            return;

        try
        {
            if (args.ApplicationMessage.UserProperties is null)
            {
                UserPropertiesMissing(_logger, args.ApplicationMessage.Topic);
                return;
            }

            var timestamp = args.ApplicationMessage.FindRequiredUserProperty(MqttUserProperties.Timestamp).GetDateTime();

            await EventReceivedAsync.Invoke(new DataflowEventArgs(eventId, args.ApplicationMessage, timestamp));
        }
        catch (Exception ex)
        {
            LogFailedToProcessClusterEvent(_logger, ex, eventId);
        }
    }

    public async Task Subscribe(IReadOnlyCollection<IDataflowEvent> dataflowEvents, CancellationToken cancellationToken = default)
    {
        if (dataflowEvents.Count == 0)
            return;

        await _requestLock.WaitAsync(cancellationToken);
        try
        {
            await SubscribeConnected(dataflowEvents, cancellationToken);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private async Task SubscribeConnected(IReadOnlyCollection<IDataflowEvent> dataflowEvents, CancellationToken cancellationToken)
    {
        if (!await EnsureConnected(cancellationToken))
            throw new InvalidOperationException($"Failed to subscribe to {dataflowEvents.Count} topics because connection to event broker failed");

        var options = new MqttClientSubscribeOptionsBuilder();
        foreach (var dataflowEvent in dataflowEvents)
        {
            var topic = dataflowEvent.EventId.ToString();
            _activeTopics[topic] = dataflowEvent;
            options.WithTopicFilter(topic, _subscriptionServiceLevel);
        }

        var result = await _client!.SubscribeAsync(options.Build(), cancellationToken).ConfigureAwait(false);
        LogRejectedTopics(result);
    }

    private void LogRejectedTopics(MqttClientSubscribeResult? result)
    {
        if (result?.Items is null)
            return;

        foreach (var item in result.Items)
        {
            if (item.ResultCode > MqttClientSubscribeResultCode.GrantedQoS2)
                LogSubscribeResultCodeFailed(_logger, item.TopicFilter.Topic, item.ResultCode);
        }
    }

    public async Task Unsubscribe(IReadOnlyCollection<IDataflowEvent> dataflowEvents, CancellationToken cancellationToken = default)
    {
        if (dataflowEvents.Count == 0)
            return;

        await _requestLock.WaitAsync(cancellationToken);
        try
        {
            await UnsubscribeConnected(dataflowEvents, cancellationToken);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private async Task UnsubscribeConnected(IReadOnlyCollection<IDataflowEvent> dataflowEvents, CancellationToken cancellationToken)
    {
        if (!await EnsureConnected(cancellationToken))
            throw new InvalidOperationException($"Failed to unsubscribe {dataflowEvents.Count} topics because connection to event broker failed");

        var options = new MqttClientUnsubscribeOptionsBuilder();
        foreach (var dataflowEvent in dataflowEvents)
        {
            var topic = dataflowEvent.EventId.ToString();
            _activeTopics.TryRemove(topic, out _);
            options.WithTopicFilter(topic);
        }

        var result = await _client!.UnsubscribeAsync(options.Build(), cancellationToken).ConfigureAwait(false);
        if (result?.Items is not null)
        {
            foreach (var item in result.Items)
            {
                if (item.ResultCode != MqttClientUnsubscribeResultCode.Success)
                    LogUnsubscribeResultCodeFailed(_logger, item.TopicFilter, item.ResultCode);
            }
        }
    }

    public void Forget(IReadOnlyCollection<IDataflowEvent> dataflowEvents)
    {
        foreach (var dataflowEvent in dataflowEvents)
            _activeTopics.TryRemove(dataflowEvent.EventId.ToString(), out _);
    }

    public async Task PublishValue(Guid eventId, string? value, string valueType, CancellationToken cancellationToken = default)
    {
        await _requestLock.WaitAsync(cancellationToken);
        try
        {
            await PublishConnected(eventId, value, valueType, cancellationToken);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private async Task PublishConnected(Guid eventId, string? value, string valueType, CancellationToken cancellationToken)
    {
        if (!await EnsureConnected(cancellationToken))
            throw new InvalidOperationException($"Failed to set value on topic {eventId} because connection to event broker failed");

        if (!CanSerializeValueType(value, valueType, out var serializedPayload))
            return;

        var message = new MqttApplicationMessageBuilder()
            .WithQualityOfServiceLevel(_writeServiceLevel)
            .WithRetainFlag()
            .WithJsonPayload(serializedPayload)
            .WithUserProperty(MqttUserProperties.Timestamp, DateTime.UtcNow)
            .WithUserProperty(MqttUserProperties.Type, Encoding.UTF8.GetBytes(valueType))
            .WithUserProperty(MqttUserProperties.Validity, s_validityOne)
            .WithUserProperty(MqttUserProperties.EngineCycle, s_engineCycleOne)
            .WithTopic(eventId.ToString())
            .Build();

        var result = await _client!.PublishAsync(message, cancellationToken).ConfigureAwait(false);
        if (result is not null && !result.IsSuccess)
            LogPublishResultCodeFailed(_logger, eventId, result.ReasonCode);
    }

    /// <summary>
    /// value type with AssemblyQualifiedName
    /// </summary>
    /// <param name="value"></param>
    /// <param name="valueType"></param>
    /// <param name="serialized"></param>
    /// <returns></returns>
    private bool CanSerializeValueType(string? value, string valueType, [NotNullWhen(true)] out byte[]? serialized)
    {
        serialized = null;

        try
        {
            var type = Type.GetType(valueType);
            if (type is null)
            {
                LogFailedToGetType(_logger, valueType);
                return false;
            }

            // we have to convert the type because we can't serialize string to double
            var typedValue = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);

            serialized = JsonSerializer.SerializeToUtf8Bytes(typedValue, type, DefaultJsonSerializerSettings.Default);
            return true;
        }
        catch (Exception ex)
        {
            LogFailedToSerializeType(_logger, ex, valueType);
        }
        return false;
    }

    private void StartReconnectLoop()
    {
        if (_reconnectLoopTask is not null || _client is not { } client || _options is not { } options)
            return;

        _reconnectCancellation = new CancellationTokenSource();
        var token = _reconnectCancellation.Token;
        var timer = _reconnectTimer = new PeriodicTimer(s_reconnectInterval);

        // The loop keeps its own references: a reset stops it while it may still be starting, and the fields belong to
        // the next connection by then.
        _reconnectLoopTask = Task.Run(async () =>
        {
            LogReconnectLoopStarted(_logger, ReconnectIntervalSeconds);
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    if (token.IsCancellationRequested)
                        break;

                    try
                    {
                        if (!await client.TryPingAsync(token))
                            await Reconnect(client, options, token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Shutting down
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogReconnectAttemptFailed(_logger, ex, ex.Message);
                        // Swallow to retry next tick.
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
            }
            finally
            {
                LogReconnectLoopStopped(_logger);
            }
        }, token);
    }

    private async Task Reconnect(IMqttClient client, MqttClientOptions options, CancellationToken cancellationToken)
    {
        await _requestLock.WaitAsync(cancellationToken);
        try
        {
            // A request may have reconnected while this one waited.
            if (await client.TryPingAsync(cancellationToken))
                return;

            await client.ConnectAsync(options, cancellationToken);
            await ResubscribeActiveTopics(cancellationToken);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    // The loop is awaited before its timer is disposed, and the loop is cleared whatever it ended with, so a failed loop
    // can neither break the disconnect nor keep the next connection from starting its own.
    private async Task StopReconnectLoop()
    {
        if (_reconnectCancellation is null)
            return;

        try
        {
            await _reconnectCancellation.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
            // ignore
        }

        try
        {
            if (_reconnectLoopTask is not null)
                await _reconnectLoopTask;
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            LogReconnectLoopFailed(_logger, ex);
        }
        finally
        {
            _reconnectLoopTask = null;

            _reconnectTimer?.Dispose();
            _reconnectTimer = null;

            _reconnectCancellation.Dispose();
            _reconnectCancellation = null;
        }
    }

    // One packet for every tracked topic, so a reconnect holds up the requests behind it for one round trip only.
    private async Task ResubscribeActiveTopics(CancellationToken cancellationToken)
    {
        var topics = _activeTopics.Keys.ToList();
        if (topics.Count == 0)
            return;

        var options = new MqttClientSubscribeOptionsBuilder();
        foreach (var topic in topics)
            options.WithTopicFilter(topic, _subscriptionServiceLevel);

        try
        {
            var result = await _client!.SubscribeAsync(options.Build(), cancellationToken).ConfigureAwait(false);
            LogResubscribedTopics(_logger, topics.Count);
            LogRejectedTopics(result);
        }
        catch (Exception ex)
        {
            LogResubscribeTopicsFailed(_logger, ex, topics.Count);
        }
    }

    public async Task ResetAndReconnect(CancellationToken cancellationToken = default)
    {
        try
        {
            await _requestLock.WaitAsync(cancellationToken);
            try
            {
                await Disconnect();

                _options = null;
                mqttProvider.ResetConnection();

                await EnsureConnected(cancellationToken);
            }
            finally
            {
                _requestLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or CancellationTokenSource already disposed, nothing we can do, return gracefully
        }
    }

    /// <summary>
    /// copy of https://gitlab.com/vicione-oss/vicione/runtime/engine-host/-/blob/master/src/CommunicationProviders/Communication/MqttUserProperties.cs
    /// </summary>
    internal static class MqttUserProperties
    {
        internal const string Timestamp = nameof(Timestamp);
        internal const string Validity = nameof(Validity);
        internal const string EngineCycle = nameof(EngineCycle);
        internal const string Id = nameof(Id);
        internal const string Name = nameof(Name);
        internal const string Type = nameof(Type);
    }

    // Source-generated logging methods
    [LoggerMessage(LogLevel.Warning, "Could not connect to dataflow events because of '{Status}'")]
    private static partial void LogCouldNotConnectToDataflowEvents(ILogger logger, MqttClientConnectResultCode status);

    [LoggerMessage(LogLevel.Error, "Could not connect to dataflow events")]
    private static partial void LogCouldNotConnectToDataflowEventsError(ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Failed to process cluster event {EventId}")]
    private static partial void LogFailedToProcessClusterEvent(ILogger logger, Exception exception, Guid eventId);

    [LoggerMessage(LogLevel.Warning, "Failed to get type for {ValueType}")]
    private static partial void LogFailedToGetType(ILogger logger, string valueType);

    [LoggerMessage(LogLevel.Error, "Failed to serialize type {ValueType}")]
    private static partial void LogFailedToSerializeType(ILogger logger, Exception exception, string valueType);

    [LoggerMessage(LogLevel.Information, "Reconnect loop started (interval: {IntervalSeconds}s).")]
    private static partial void LogReconnectLoopStarted(ILogger logger, int intervalSeconds);

    [LoggerMessage(LogLevel.Debug, "Reconnect attempt failed: {Message}")]
    private static partial void LogReconnectAttemptFailed(ILogger logger, Exception exception, string message);

    [LoggerMessage(LogLevel.Information, "Reconnect loop stopped.")]
    private static partial void LogReconnectLoopStopped(ILogger logger);

    [LoggerMessage(LogLevel.Warning, "Reconnect loop failed.")]
    private static partial void LogReconnectLoopFailed(ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Received message on '{Topic}' without UserProperties.")]
    private static partial void UserPropertiesMissing(ILogger logger, string topic);

    [LoggerMessage(LogLevel.Debug, "Resubscribed to {Count} topics after reconnect.")]
    private static partial void LogResubscribedTopics(ILogger logger, int count);

    [LoggerMessage(LogLevel.Warning, "Failed to resubscribe to {Count} topics after reconnect.")]
    private static partial void LogResubscribeTopicsFailed(ILogger logger, Exception exception, int count);

    [LoggerMessage(LogLevel.Warning, "MQTT subscribe failed for topic '{Topic}' with result code {ResultCode}.")]
    private static partial void LogSubscribeResultCodeFailed(ILogger logger, string topic, MqttClientSubscribeResultCode resultCode);

    [LoggerMessage(LogLevel.Warning, "MQTT unsubscribe failed for topic '{Topic}' with result code {ResultCode}.")]
    private static partial void LogUnsubscribeResultCodeFailed(ILogger logger, string topic, MqttClientUnsubscribeResultCode resultCode);

    [LoggerMessage(LogLevel.Warning, "MQTT publish failed for event '{EventId}' with reason code {ReasonCode}.")]
    private static partial void LogPublishResultCodeFailed(ILogger logger, Guid eventId, MqttClientPublishReasonCode reasonCode);
}
