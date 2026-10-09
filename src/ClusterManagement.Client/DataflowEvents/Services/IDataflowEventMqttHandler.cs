using ClusterManagement.Public.DataflowEvents;
using MQTTnet;

namespace ClusterManagement.Client.DataflowEvents.Services;

/// <summary>
/// Manages the MQTT connection used to subscribe to and publish dataflow event topics.
/// </summary>
internal interface IDataflowEventMqttHandler
{
    /// <summary>
    /// Raised when a message is received on a subscribed dataflow event topic.
    /// </summary>
    event Func<DataflowEventArgs, Task>? EventReceivedAsync;

    /// <summary>
    /// Unsubscribes all active topics, stops the reconnect loop, and disconnects the underlying MQTT client.
    /// </summary>
    Task Disconnect();

    /// <summary>
    /// Serializes <paramref name="value"/> to the given <paramref name="valueType"/> and publishes it
    /// as a retained MQTT message on the topic identified by <paramref name="eventId"/>.
    /// </summary>    
    Task PublishValue(Guid eventId, string? value, string valueType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the MQTT client is connected and subscribes to the topics corresponding to <paramref name="dataflowEvents"/>
    /// with one SUBSCRIBE packet. The topics are tracked so they can be automatically resubscribed after a reconnect,
    /// also when the packet fails.
    /// </summary>
    Task Subscribe(IReadOnlyCollection<IDataflowEvent> dataflowEvents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the MQTT client is connected and unsubscribes from the topics corresponding to <paramref name="dataflowEvents"/>
    /// with one UNSUBSCRIBE packet. The topics are removed from the active-topics tracking list.
    /// </summary>
    Task Unsubscribe(IReadOnlyCollection<IDataflowEvent> dataflowEvents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops tracking the topics of <paramref name="dataflowEvents"/> without sending a packet, so a reconnect does not
    /// subscribe them again. For topics whose SUBSCRIBE packet failed and that are no longer needed.
    /// </summary>
    void Forget(IReadOnlyCollection<IDataflowEvent> dataflowEvents);

    /// <summary>
    /// Disconnects and fully resets the MQTT client and connection options, then reconnects and
    /// resubscribes all previously active topics. Concurrent calls are serialized via an internal semaphore.
    /// </summary>
    Task ResetAndReconnect(CancellationToken cancellationToken = default);
}

public class DataflowEventArgs(Guid eventId, MqttApplicationMessage message, DateTime timestamp) : EventArgs
{
    public Guid EventId { get; private set; } = eventId;

    public MqttApplicationMessage Message { get; private set; } = message;

    public DateTime Timestamp { get; private set; } = timestamp;
}
