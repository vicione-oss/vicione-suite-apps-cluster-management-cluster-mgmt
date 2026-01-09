using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace ClusterManagement.Backend.Mqtt;

internal sealed class MqttNetObserverClient(IOptions<MqttNetRpcClientOptions> options, IMqttBrokerConnectionCache connectionProvider) :
    IMqttObserverClient, IDisposable
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<MqttObserverResponse>> _waitingCalls = new(StringComparer.OrdinalIgnoreCase);
    private readonly IOptions<MqttNetRpcClientOptions> _options = options;
    private readonly IMqttBrokerConnectionCache _connectionProvider = connectionProvider;
    private readonly IMqttClient? _mqttClient;

    internal MqttNetObserverClient(IOptions<MqttNetRpcClientOptions> options, IMqttClient? mqttClient = default)
        : this(options, default(IMqttBrokerConnectionCache)!)
        => _mqttClient = mqttClient;

    public async Task<MqttObserverResponse> Observe(Func<CancellationToken, Task> action, string topic, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic is invalid.", nameof(topic));

        var mqttClient = await _connectionProvider.ConnectClient(_mqttClient, HandleApplicationMessageReceived, cancellationToken, $"cm-observer-{Guid.NewGuid()}").ConfigureAwait(false);

        using CancellationTokenSource timeOutTokenSource = new();
        try
        {
            TaskCompletionSource<MqttObserverResponse> awaitable = new(TaskCreationOptions.RunContinuationsAsynchronously);
            awaitable = _waitingCalls.GetOrAdd(topic, awaitable);

            var subscribeOptions = new MqttClientSubscribeOptionsBuilder().WithTopicFilter(topic, MqttQualityOfServiceLevel.ExactlyOnce).Build();
            await mqttClient.SubscribeAsync(subscribeOptions, cancellationToken).ConfigureAwait(false);
            await action(cancellationToken).ConfigureAwait(false);

            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeOutTokenSource.Token);
            timeOutTokenSource.CancelAfter(_options.Value.RequestTimeout);
            using (linkedTokenSource.Token.Register(() => awaitable.TrySetCanceled()))
            {
                return await awaitable.Task.ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException ex)
        {
            if (timeOutTokenSource.IsCancellationRequested)
                throw new TaskCanceledException($"No message could be observed within {_options.Value.RequestTimeout}.", ex, timeOutTokenSource.Token);
            throw;
        }
        finally
        {
            _waitingCalls.TryRemove(topic, out _);

            await mqttClient.UnsubscribeAsync(topic, cancellationToken).ConfigureAwait(false);

            await mqttClient.DisconnectClient(_mqttClient, HandleApplicationMessageReceived, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task HandleApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        if (_waitingCalls.TryRemove(eventArgs.ApplicationMessage.Topic, out var awaitable))
        {
            string? payload = default;
            if (eventArgs.ApplicationMessage.Payload.Length > 0)
                payload = eventArgs.ApplicationMessage.ConvertPayloadToString();
            MqttObserverResponse response = new(payload);
            awaitable.TrySetResult(response);
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
