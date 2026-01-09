using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using ClusterManagement.Client.Management.Extensions;
using ClusterManagement.Public.DataflowEvents;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using Sdk.Messaging;

namespace ClusterManagement.Client.DataflowEvents.Services;

internal sealed class DataflowEventMqttHandler(IDataflowEventMqttClientProvider mqttProvider, ILogger<DataflowEventMqttHandler> logger) : IDataflowEventMqttHandler, IDisposable
{
    private readonly ILogger<DataflowEventMqttHandler> _logger = logger;
    private readonly MqttQualityOfServiceLevel _subscriptionServiceLevel = MqttQualityOfServiceLevel.AtMostOnce;
    private readonly MqttQualityOfServiceLevel _writeServiceLevel = MqttQualityOfServiceLevel.AtMostOnce;

    private IMqttClient? _client;

    public event Func<DataflowEventArgs, Task>? EventReceivedAsync;

    public async Task Disconnect()
    {
        if (_client is null)
            return;

        _client.ApplicationMessageReceivedAsync -= HandleApplicationMessageReceived;

        if (_client.IsConnected)
            await _client.DisconnectAsync();

        _client.Dispose();
        _client = null;
    }

    public void Dispose() => _client?.Dispose();

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

            var options = await mqttProvider.CreateSystemMqttClientOptions();
            var result = await _client.ConnectAsync(options, cancellationToken);
            if (result.ResultCode == MqttClientConnectResultCode.Success)
                return true;

            _logger.LogWarning("Could not connect to dataflow events because of '{Status}'", result.ResultCode);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not connect to dataflow events");
        }
        return false;
    }

    private async Task HandleApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs args)
    {
        if (EventReceivedAsync is null)
            return;

        if (!Guid.TryParse(args.ApplicationMessage.Topic, out var eventId))
            return;

        try
        {
            ArgumentNullException.ThrowIfNull(args.ApplicationMessage.UserProperties, nameof(args.ApplicationMessage.UserProperties));

            var timestamp = args.ApplicationMessage.FindRequiredUserProperty(MqttUserProperties.Timestamp).GetDateTime();

            await EventReceivedAsync.Invoke(new DataflowEventArgs(eventId, args.ApplicationMessage, timestamp));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process cluster event {EventId}", eventId);
        }
    }

    public async Task Subscribe(IDataflowEvent dataflowEvent, CancellationToken cancellationToken = default)
    {
        if (!await EnsureConnected(cancellationToken))
            throw new InvalidOperationException($"Failed to subscribe to topic {dataflowEvent.EventId} because connection to event broker failed");

        // todo check result items for success?
        _ = await _client!.SubscribeAsync(dataflowEvent.EventId.ToString(), _subscriptionServiceLevel, cancellationToken).ConfigureAwait(false);
    }

    public async Task Unsubscribe(IDataflowEvent dataflowEvent, CancellationToken cancellationToken = default)
    {
        if (!await EnsureConnected(cancellationToken))
            throw new InvalidOperationException($"Failed to unsubscribe topic {dataflowEvent.EventId} because connection to event broker failed");

        // todo check result items for success?
        _ = await _client!.UnsubscribeAsync(dataflowEvent.EventId.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public async Task PublishValue(Guid eventId, string? value, string valueType, CancellationToken cancellationToken = default)
    {
        if (!await EnsureConnected(cancellationToken))
            throw new InvalidOperationException($"Failed to set value on topic {eventId} because connection to event broker failed");

        if (!CanSerializeValueType(value, valueType, out var serializedPaylod))
            return;

        var message = new MqttApplicationMessageBuilder()
            .WithQualityOfServiceLevel(_writeServiceLevel)
            .WithRetainFlag()
            .WithJsonPayload(serializedPaylod)
            .WithUserProperty(MqttUserProperties.Timestamp, DateTime.UtcNow)
            .WithUserProperty(MqttUserProperties.Type, valueType)
            .WithUserProperty(MqttUserProperties.Validity, "1")
            .WithUserProperty(MqttUserProperties.EngineCycle, "1")
            .WithTopic(eventId.ToString())
            .Build();

        // todo check result items for success?
        _ = await _client!.PublishAsync(message, cancellationToken).ConfigureAwait(false);
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
                _logger.LogWarning("Failed to get type for {ValueType}", valueType);
                return false;
            }

            // we have to convert the type because we can't serialize string to double
            var typedValue = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);

            serialized = JsonSerializer.SerializeToUtf8Bytes(typedValue, type, DefaultJsonSerializerSettings.Default);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to serialize type {ValueType}", valueType);
        }
        return false;
    }

    /// <summary>
    /// copy of https://gitlab.i40.ifm-datalink.net/acx/vo-suite/engine-host/-/blob/master/src/CommunicationProviders/Communication/MqttUserProperties.cs
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
}
