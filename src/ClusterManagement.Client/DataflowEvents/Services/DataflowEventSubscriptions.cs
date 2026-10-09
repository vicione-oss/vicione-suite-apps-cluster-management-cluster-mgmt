namespace ClusterManagement.Client.DataflowEvents.Services;

/// <summary>
/// What subscribing several events at once achieved: the subscription of each subscribed event and the reason each
/// other event could not be subscribed.
/// </summary>
public sealed record DataflowEventSubscriptions(IReadOnlyDictionary<Guid, IAsyncDisposable> Subscriptions, IReadOnlyDictionary<Guid, Exception> Failures);
