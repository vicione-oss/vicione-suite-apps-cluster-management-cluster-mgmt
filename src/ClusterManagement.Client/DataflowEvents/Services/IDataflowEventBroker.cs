using ClusterManagement.Client.DataflowEvents.Models;
using ClusterManagement.Public.DataflowEvents;
using ClusterManagement.Public.Requests;

namespace ClusterManagement.Client.DataflowEvents.Services;

public interface IDataflowEventBroker : IEventBroker
{
    Task<ClusterInfo?> GetCurrentClusterInfo();

    /// <summary>
    /// Raised in the background after a deployment changed the running version, once the broker has dropped the events of
    /// the previous one; <see cref="IEventBroker.GetEvents(CancellationToken)"/> and <see cref="GetCurrentClusterInfo"/>
    /// then load those of the new version.
    /// </summary>
    event Func<Task>? EventsChanged;

    /// <summary>
    /// Subscribes each event with its handler like <see cref="IEventBroker.Subscribe(Guid, Func{DateTime, string?, Task}, CancellationToken)"/>,
    /// with one request to the event broker for all events that had no subscription yet. A handler for an event whose
    /// request is still under way waits for that request and fails with it. An event that cannot be subscribed does not stop
    /// the others; it is listed with its reason in <see cref="DataflowEventSubscriptions.Failures"/>, also when the call is
    /// cancelled, so the subscriptions it did make are not lost.
    /// </summary>
    Task<DataflowEventSubscriptions> Subscribe(IReadOnlyDictionary<Guid, Func<DateTime, string?, Task>> handlers, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends subscriptions of this broker like disposing each of them, with one request to the event broker for all events
    /// that are left without a subscription. The subscriptions end even when that request fails.
    /// </summary>
    Task Unsubscribe(IReadOnlyCollection<IAsyncDisposable> subscriptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// What came along with the last value of the event, or <c>null</c> before its first value. A handler reads it for the
    /// value it is called with. A broker that does not keep them has none.
    /// </summary>
    DataflowValueDetails? DetailsOf(Guid eventId) => null;
}
