namespace ClusterManagement.Backend.Mqtt;

public interface IMqttObserverClient
{
    Task<MqttObserverResponse> Observe(Func<CancellationToken, Task> action, string topic, CancellationToken cancellationToken);
}

public sealed record MqttObserverResponse(string? Payload = default);
