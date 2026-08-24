using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace ClusterManagement.Backend.Mqtt;

internal sealed class MqttNetRpcClient(IOptions<MqttNetRpcClientOptions> options, IMqttBrokerConnectionCache connectionProvider) :
    IMqttRpcClient, IDisposable
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<MqttRpcResponse>> _waitingCalls = new();
    private readonly IOptions<MqttNetRpcClientOptions> _options = options;
    private readonly IMqttBrokerConnectionCache _connectionProvider = connectionProvider;
    private readonly IMqttClient? _mqttClient;

    internal MqttNetRpcClient(IOptions<MqttNetRpcClientOptions> options, IMqttClient? mqttClient = default)
        : this(options, default(IMqttBrokerConnectionCache)!)
        => _mqttClient = mqttClient;

    public async Task<MqttRpcResponse> Request(string requestTopic, MqttRpcRequest request, string responseTopic,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestTopic))
            throw new ArgumentException("RPC request topic is invalid.", nameof(requestTopic));
        if (string.IsNullOrWhiteSpace(responseTopic))
            throw new ArgumentException("RPC response topic is invalid.", nameof(responseTopic));
        if (string.IsNullOrWhiteSpace(request.Method))
            throw new ArgumentException("RPC method is invalid.", nameof(request));

        var mqttClient = await _connectionProvider.ConnectClient(_mqttClient, HandleApplicationMessageReceived, cancellationToken, $"cm-rpc-{Guid.NewGuid()}");

        var correlationData = Guid.NewGuid();
        var requestMessageBuilder = new MqttApplicationMessageBuilder()
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
            .WithCorrelationData(correlationData.ToByteArray())
            .WithTopic(requestTopic)
            .WithUserProperty("Method", Encoding.UTF8.GetBytes(request.Method));
        if (request.Payload.HasValue)
        {
            requestMessageBuilder = requestMessageBuilder.WithPayload(request.Payload.Value)
                .WithContentType(request.ContentType);
        }
        var requestMessage = requestMessageBuilder.Build();

        using CancellationTokenSource responseTimeOutTokenSource = new();
        try
        {
            TaskCompletionSource<MqttRpcResponse> awaitable = new(TaskCreationOptions.RunContinuationsAsynchronously);

            _waitingCalls.TryAdd(correlationData, awaitable);

            var subscribeOptions = new MqttClientSubscribeOptionsBuilder().WithTopicFilter(responseTopic, MqttQualityOfServiceLevel.ExactlyOnce).Build();
            await mqttClient.SubscribeAsync(subscribeOptions, cancellationToken).ConfigureAwait(false);
            await mqttClient.PublishAsync(requestMessage, cancellationToken).ConfigureAwait(false);

            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, responseTimeOutTokenSource.Token);
            responseTimeOutTokenSource.CancelAfter(_options.Value.RequestTimeout);
            using (linkedTokenSource.Token.Register(() => awaitable.TrySetCanceled()))
            {
                return await awaitable.Task.ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException ex)
        {
            if (responseTimeOutTokenSource.IsCancellationRequested)
                throw new TaskCanceledException($"The MQTT RPC server did not respond within {_options.Value.RequestTimeout}.", ex, responseTimeOutTokenSource.Token);
            throw;
        }
        finally
        {
            _waitingCalls.TryRemove(correlationData, out _);

            await mqttClient.UnsubscribeAsync(responseTopic, CancellationToken.None).ConfigureAwait(false);

            await mqttClient.DisconnectClient(_mqttClient, HandleApplicationMessageReceived, cancellationToken);
        }
    }

    private Task HandleApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        var correlationData = Guid.Empty;
        try
        {
            correlationData = new(eventArgs.ApplicationMessage.CorrelationData);
        }
        catch
        {
            // no valid byte array to convert to Guid
            eventArgs.IsHandled = true;
            return Task.CompletedTask;
        }
        if (_waitingCalls.TryRemove(correlationData, out var awaitable))
        {
            var status = eventArgs.ApplicationMessage.UserProperties.Find(static p => string.Equals(p.Name, "Status", StringComparison.Ordinal))?.ReadValueAsString();
            if (status is null)
            {
                awaitable.TrySetException(new InvalidOperationException("MqttRpcResponse does not contain status."));
            }
            else
            {
                MqttRpcResponse response = new(status, eventArgs.ApplicationMessage.Payload, eventArgs.ApplicationMessage.ContentType);
                awaitable.TrySetResult(response);
            }
        }

        eventArgs.IsHandled = true;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var waitingCall in _waitingCalls)
            waitingCall.Value.TrySetCanceled();

        _waitingCalls.Clear();
    }
}
