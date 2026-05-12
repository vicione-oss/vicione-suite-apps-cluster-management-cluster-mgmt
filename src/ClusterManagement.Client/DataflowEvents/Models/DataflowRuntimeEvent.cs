using System.Collections.Immutable;

namespace ClusterManagement.Client.DataflowEvents.Models;

internal sealed class DataflowRuntimeEvent
{
    internal required Guid EventId { get; init; }
    internal DateTime? Timestamp { get; set; }
    internal string? Value { get; set; }
    internal ReadOnlyMemory<byte> LastPayload { get; set; }

    private ImmutableArray<Func<DateTime, string?, Task>> _subscriptions = [];

    internal ImmutableArray<Func<DateTime, string?, Task>> Subscriptions => _subscriptions;

    /// <summary>Returns new length after add (thread-safe CAS).</summary>
    internal int AddSubscription(Func<DateTime, string?, Task> handler)
    {
        ImmutableArray<Func<DateTime, string?, Task>> before, after;
        do
        {
            before = _subscriptions;
            after = before.Add(handler);
        } while (ImmutableInterlocked.InterlockedCompareExchange(ref _subscriptions, after, before) != before);
        return after.Length;
    }

    /// <summary>Returns true when the last subscriber was removed (1→0).</summary>
    internal bool RemoveSubscription(Func<DateTime, string?, Task> handler)
    {
        ImmutableArray<Func<DateTime, string?, Task>> before, after;
        do
        {
            before = _subscriptions;
            after = before.Remove(handler);
            if (after == before) return false;
        } while (ImmutableInterlocked.InterlockedCompareExchange(ref _subscriptions, after, before) != before);
        return after.IsEmpty;
    }
}
