namespace ClusterManagement.Client.DataflowEvents.Models;

internal sealed class DataflowRuntimeEvent : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly List<Func<DateTime, string?, Task>> _subscriptions = [];

    internal required Guid EventId { get; init; }
    internal DateTime? Timestamp { get; set; }
    internal string? Value { get; set; }

    internal int SubscriptionCount
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _subscriptions.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    internal void AddSubscription(Func<DateTime, string?, Task> handler)
    {
        _lock.EnterWriteLock();
        try
        {
            _subscriptions.Add(handler);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    internal bool RemoveSubscription(Func<DateTime, string?, Task> handler)
    {
        _lock.EnterWriteLock();
        try
        {
            return _subscriptions.Remove(handler);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    internal List<Func<DateTime, string?, Task>> GetSubscriptionSnapshot()
    {
        _lock.EnterReadLock();
        try
        {
            return [.. _subscriptions];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Dispose()
        => _lock?.Dispose();
}
