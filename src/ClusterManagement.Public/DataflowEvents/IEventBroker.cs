namespace ClusterManagement.Public.DataflowEvents;

public interface IEventBroker
{
    /// <summary>
    /// Gets all Events available in the currently active cluster configuration
    /// </summary>
    /// <returns>List of Events available in the currently active cluster configuration</returns>
    Task<IReadOnlyCollection<IDataflowEvent>> GetEvents();

    /// <summary>
    /// Begin monitoring a dataflow event
    /// </summary>
    /// <param name="eventId">The ID of the event to monitor</param>
    /// <param name="handler">The handler to call for every value.</param>
    /// <returns>The handle which will be updated when the value on the broker changes</returns>
    Task<IAsyncDisposable> Subscribe(Guid eventId, Func<DateTime, string?, Task> handler);

    /// <summary>
    /// Get a handle which will be used to set a value on the broker
    /// </summary>
    /// <param name="eventId">The ID of the event to set a value for</param>
    /// <param name="value">The value to set.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The handle which will be used to set a value on the broker</returns>
    Task SetValue(Guid eventId, string? value, CancellationToken cancellationToken = default);
}
