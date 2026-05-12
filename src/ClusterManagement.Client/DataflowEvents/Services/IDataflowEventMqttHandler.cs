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
    /// <param name="eventId">The event whose topic will be published to.</param>
    /// <param name="value">The string representation of the value to publish.</param>
    /// <param name="valueType">The assembly-qualified type name used to deserialize and re-serialize the value.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task PublishValue(Guid eventId, string? value, string valueType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the MQTT client is connected and subscribes to the topic corresponding to <paramref name="dataflowEvent"/>.
    /// The topic is tracked so it can be automatically resubscribed after a reconnect.
    /// </summary>
    /// <param name="dataflowEvent">The dataflow event whose topic should be subscribed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task Subscribe(IDataflowEvent dataflowEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the MQTT client is connected and unsubscribes from the topic corresponding to <paramref name="dataflowEvent"/>.
    /// The topic is removed from the active-topics tracking list.
    /// </summary>
    /// <param name="dataflowEvent">The dataflow event whose topic should be unsubscribed.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task Unsubscribe(IDataflowEvent dataflowEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects and fully resets the MQTT client and connection options, then reconnects and
    /// resubscribes all previously active topics. Concurrent calls are serialized via an internal semaphore.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task ResetAndReconnect(CancellationToken cancellationToken = default);
}

public class DataflowEventArgs(Guid eventId, MqttApplicationMessage message, DateTime timestamp) : EventArgs
{
    public Guid EventId { get; private set; } = eventId;

    public MqttApplicationMessage Message { get; private set; } = message;

    public DateTime Timestamp { get; private set; } = timestamp;
}
