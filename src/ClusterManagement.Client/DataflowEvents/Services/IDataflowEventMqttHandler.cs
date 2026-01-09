using ClusterManagement.Public.DataflowEvents;
using MQTTnet;

namespace ClusterManagement.Client.DataflowEvents.Services;

internal interface IDataflowEventMqttHandler
{
    event Func<DataflowEventArgs, Task>? EventReceivedAsync;

    Task Disconnect();

    Task PublishValue(Guid eventId, string? value, string valueType, CancellationToken cancellationToken = default);

    Task Subscribe(IDataflowEvent dataflowEvent, CancellationToken cancellationToken = default);

    Task Unsubscribe(IDataflowEvent dataflowEvent, CancellationToken cancellationToken = default);
}

public class DataflowEventArgs(Guid eventId, MqttApplicationMessage message, DateTime timestamp) : EventArgs
{
    public Guid EventId { get; private set; } = eventId;

    public MqttApplicationMessage Message { get; private set; } = message;

    public DateTime Timestamp { get; private set; } = timestamp;
}
