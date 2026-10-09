using System.Collections.Immutable;

namespace ClusterManagement.Client.DataflowEvents.Models;

internal sealed class DataflowRuntimeEvent
{
    internal required Guid EventId { get; init; }
    internal DateTime? Timestamp { get; set; }
    internal string? Value { get; set; }
    internal ReadOnlyMemory<byte> LastPayload { get; set; }

    private readonly Lock _lock = new();
    private ImmutableArray<Func<DateTime, string?, Task>> _subscriptions = [];
    private TaskCompletionSource _topicRequest = new();

    internal ImmutableArray<Func<DateTime, string?, Task>> Subscriptions => _subscriptions;

    /// <summary>
    /// Adds <paramref name="handler"/> and returns the request for the event's topic, which every handler has to wait
    /// for. The first handler (<paramref name="isFirst"/>) starts a new request and reports how it ended with
    /// <see cref="EndTopicRequest"/>.
    /// </summary>
    internal Task AddSubscription(Func<DateTime, string?, Task> handler, out bool isFirst)
    {
        lock (_lock)
        {
            isFirst = _subscriptions.IsEmpty;
            if (isFirst)
                _topicRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            _subscriptions = _subscriptions.Add(handler);
            return _topicRequest.Task;
        }
    }

    /// <param name="failure">Why the topic could not be subscribed, or <c>null</c> when it is.</param>
    internal void EndTopicRequest(Exception? failure)
    {
        lock (_lock)
        {
            if (failure is null)
                _topicRequest.TrySetResult();
            else
                _topicRequest.TrySetException(failure);
        }
    }

    /// <summary>Returns true when the last subscriber was removed (1→0).</summary>
    internal bool RemoveSubscription(Func<DateTime, string?, Task> handler)
    {
        lock (_lock)
        {
            var after = _subscriptions.Remove(handler);
            if (after == _subscriptions)
                return false;

            _subscriptions = after;
            return after.IsEmpty;
        }
    }
}
