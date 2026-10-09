using Microsoft.Extensions.Logging;

namespace ClusterManagement.Client.DataflowEvents.Services;

/// <summary>
/// Keeps the outputs the table shows subscribed. What is shown counts once it has held for <see cref="SettleTime"/>, so
/// scrolling sends one request instead of one per row; an output that left the table keeps its subscription for
/// <see cref="KeepTime"/>, so scrolling back and forth does not end and renew it. The event broker retains the last value
/// of every output, so a new subscription shows the current value at once. The page calls it on its renderer's
/// dispatcher, where its loop continues as well, so it needs no lock.
/// </summary>
internal sealed partial class ShownEventSubscriptions(
    IDataflowEventBroker broker,
    Func<Guid, Func<DateTime, string?, Task>> handlerFor,
    TimeProvider timeProvider,
    ILogger logger) : IAsyncDisposable
{
    internal static TimeSpan SettleTime { get; } = TimeSpan.FromMilliseconds(300);
    internal static TimeSpan KeepTime { get; } = TimeSpan.FromSeconds(5);
    internal static TimeSpan RetryTime { get; } = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<Guid, IAsyncDisposable> _subscriptions = [];
    private readonly Dictionary<Guid, DateTimeOffset> _hiddenSince = [];
    private readonly Dictionary<Guid, DateTimeOffset> _retryAt = [];
    private HashSet<Guid> _shown = [];
    private DateTimeOffset _shownAt;
    private Task _apply = Task.CompletedTask;
    private Action? _wake;

    /// <summary>
    /// The outputs the table shows now, replacing the ones shown before.
    /// </summary>
    public void Show(IEnumerable<Guid> eventIds)
    {
        HashSet<Guid> shown = [.. eventIds];
        if (shown.SetEquals(_shown))
            return;

        var now = timeProvider.GetUtcNow();
        _shown = shown;
        _shownAt = now;

        foreach (var eventId in _subscriptions.Keys.Where(id => !_shown.Contains(id)))
            _hiddenSince.TryAdd(eventId, now);

        foreach (var eventId in _shown)
            _hiddenSince.Remove(eventId);

        foreach (var eventId in _retryAt.Keys.Except(_shown).ToList())
            _retryAt.Remove(eventId);

        if (_apply.IsCompleted)
            _apply = ApplyWhenDue(_lifetime.Token);
        else
            _wake?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync();
        await _apply;

        if (_subscriptions.Count > 0)
            await Unsubscribe([.. _subscriptions.Values], CancellationToken.None);

        _subscriptions.Clear();
        _lifetime.Dispose();
    }

    // One loop applies every change in turn, so a request for an output never overtakes an earlier one for it.
    private async Task ApplyWhenDue(CancellationToken cancellationToken)
    {
        try
        {
            while (NextChangeAt() is { } dueAt)
            {
                var wait = dueAt - timeProvider.GetUtcNow();
                if (wait > TimeSpan.Zero)
                    await WaitOrWake(wait, cancellationToken);
                else
                    await Apply(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed; the subscriptions end there.
        }
    }

    // What Show shows may be due before what the loop waits for, so Show cuts the wait short.
    private async Task WaitOrWake(TimeSpan wait, CancellationToken cancellationToken)
    {
        var wake = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _wake = wake.Cancel;
        try
        {
            await Task.Delay(wait, timeProvider, wake.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Woken by Show.
        }
        finally
        {
            _wake = null;
            wake.Dispose();
        }
    }

    // Nothing is due once every shown output is subscribed and no hidden one is left.
    private DateTimeOffset? NextChangeAt()
    {
        DateTimeOffset? next = null;
        foreach (var eventId in _shown.Where(id => !_subscriptions.ContainsKey(id)))
            next = Earlier(next, SubscribableAt(eventId));

        foreach (var hiddenSince in _hiddenSince.Values)
            next = Earlier(next, hiddenSince + KeepTime);

        return next;
    }

    private async Task Apply(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var expired = _hiddenSince.Where(h => h.Value + KeepTime <= now).Select(h => h.Key).ToList();
        if (expired.Count > 0)
        {
            List<IAsyncDisposable> ended = [];
            foreach (var eventId in expired)
            {
                ended.Add(_subscriptions[eventId]);
                _subscriptions.Remove(eventId);
                _hiddenSince.Remove(eventId);
            }

            await Unsubscribe(ended, cancellationToken);
        }

        var due = _shown.Where(id => !_subscriptions.ContainsKey(id) && SubscribableAt(id) <= now).ToList();
        if (due.Count > 0)
            await Subscribe(due, cancellationToken);
    }

    // An event that failed waits for its retry; the others only for the table to settle.
    private DateTimeOffset SubscribableAt(Guid eventId)
        => Later(_shownAt + SettleTime, _retryAt.TryGetValue(eventId, out var retryAt) ? retryAt : null);

    private async Task Subscribe(List<Guid> eventIds, CancellationToken cancellationToken)
    {
        try
        {
            var result = await broker.Subscribe(eventIds.ToDictionary(id => id, handlerFor), cancellationToken);
            foreach (var (eventId, subscription) in result.Subscriptions)
            {
                _subscriptions[eventId] = subscription;
                _retryAt.Remove(eventId);

                // The table may have moved on while the request was under way.
                if (!_shown.Contains(eventId))
                    _hiddenSince.TryAdd(eventId, timeProvider.GetUtcNow());
            }

            // The broker lists the events of a cancelled request as failures; disposing ends them without asking again.
            cancellationToken.ThrowIfCancellationRequested();

            if (result.Failures.Count > 0)
            {
                AskAgainLater(result.Failures.Keys);
                LogSubscribeFailed(logger, result.Failures.Values.First(), result.Failures.Count, eventIds.Count);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            AskAgainLater(eventIds);
            LogSubscribeFailed(logger, e, eventIds.Count, eventIds.Count);
        }
    }

    private void AskAgainLater(IEnumerable<Guid> eventIds)
    {
        var retryAt = timeProvider.GetUtcNow() + RetryTime;
        foreach (var eventId in eventIds)
            _retryAt[eventId] = retryAt;
    }

    // The broker drops the handlers before it asks the event broker, so the values stop even when that request fails.
    private async Task Unsubscribe(List<IAsyncDisposable> subscriptions, CancellationToken cancellationToken)
    {
        try
        {
            await broker.Unsubscribe(subscriptions, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogUnsubscribeFailed(logger, e, subscriptions.Count);
        }
    }

    private static DateTimeOffset Later(DateTimeOffset value, DateTimeOffset? other)
        => other is { } o && o > value ? o : value;

    private static DateTimeOffset Earlier(DateTimeOffset? value, DateTimeOffset other)
        => value is { } v && v < other ? v : other;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to subscribe {FailedCount} of {Count} shown events; asking again shortly")]
    private static partial void LogSubscribeFailed(ILogger logger, Exception exception, int failedCount, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to unsubscribe {Count} events that are no longer shown")]
    private static partial void LogUnsubscribeFailed(ILogger logger, Exception exception, int count);
}
