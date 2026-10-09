using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Text;
using ClusterManagement.Client.DataflowEvents.Models;
using ClusterManagement.Public.DataflowEvents;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Utils;

namespace ClusterManagement.Client.DataflowEvents.Services;

internal sealed partial class DataflowEventBroker : IDataflowEventBroker,
    IEventConsumer<ClusterUpdateCompleted>,
    IAsyncDisposable
{
    private readonly IUiMediator _mediator;
    private readonly IDataflowEventMqttHandler _mqttHandler;
    private readonly ILogger<DataflowEventBroker> _logger;
    private readonly SemaphoreSlim _cacheSemaphore = new(1, 1);
    private Dictionary<Guid, IDataflowEvent>? _availableEvents;
    private ClusterInfo? _currentClusterInfo;
    private readonly ConcurrentDictionary<Guid, DataflowRuntimeEvent> _dataflowRuntimeEvents = [];
    private readonly AutoDisposeList<IDisposable> _mediatorHandles = [];
    private CancellationTokenSource? _reconnectCts;
    private Task? _reconnectTask;
    private volatile bool _isCacheInvalid;

    public event Func<Task>? EventsChanged;

    public DataflowEventBroker(IUiMediator mediator, IDataflowEventMqttHandler mqttHandler, ILogger<DataflowEventBroker> logger)
    {
        _mediator = mediator;
        _mqttHandler = mqttHandler;
        _logger = logger;

        _mqttHandler.EventReceivedAsync += OnEventReceivedAsync;
        _mediatorHandles.Add(_mediator.Register(this));
    }

    public async ValueTask DisposeAsync()
    {
        _mediatorHandles.Dispose();

        var cts = Interlocked.Exchange(ref _reconnectCts, null);
        cts?.Cancel();
        cts?.Dispose();

        var pendingReconnect = Interlocked.Exchange(ref _reconnectTask, null);
        if (pendingReconnect is not null)
        {
            try { await pendingReconnect; }
            catch (OperationCanceledException) { }
        }

        _mqttHandler.EventReceivedAsync -= OnEventReceivedAsync;

        if (_availableEvents is { } availableEvents)
        {
            List<IDataflowEvent> subscribedEvents = [.. _dataflowRuntimeEvents.Keys
                .Select(availableEvents.GetValueOrDefault)
                .OfType<IDataflowEvent>()];
            try
            {
                await _mqttHandler.Unsubscribe(subscribedEvents);
            }
            catch (Exception ex)
            {
                LogUnsubscribeDuringDisposeFailed(_logger, ex, subscribedEvents.Count);
            }
        }

        _dataflowRuntimeEvents.Clear();

        await _mqttHandler.Disconnect();

        _cacheSemaphore.Dispose();
    }

    public async Task<ClusterInfo?> GetCurrentClusterInfo()
    {
        if (_currentClusterInfo is not null)
            return _currentClusterInfo;

        await _cacheSemaphore.WaitAsync();
        try
        {
#pragma warning disable CA1508 // False positive: concurrent double-check pattern
            if (_currentClusterInfo is not null)
                return _currentClusterInfo;
#pragma warning restore CA1508

            return _currentClusterInfo = await LoadActiveClusterInfo();
        }
        finally
        {
            _cacheSemaphore.Release();
        }
    }

    private async Task<IReadOnlyCollection<IDataflowEvent>> UpdateEventCache(CancellationToken cancellationToken = default)
    {
        if (_availableEvents is not null && !_isCacheInvalid)
            return _availableEvents.Values;

        await _cacheSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_availableEvents is not null && !_isCacheInvalid)
                return _availableEvents.Values;

            _currentClusterInfo ??= await LoadActiveClusterInfo();
            if (_currentClusterInfo is null)
                return [];

            // later on we could select events related to a dataflow
            var request = new GetDataflowEventsRequest { ClusterId = _currentClusterInfo.Id };
            var response = await _mediator.Request<GetDataflowEventsRequest, GetDataflowEventsResponse>(request, cancellationToken);
            if (response.RequestError is not null)
                return [];

            var keyValuePairs = response.Results
                .OrderBy(k => k.Name)
                .Select(k => new KeyValuePair<Guid, IDataflowEvent>(k.EventId, k));

            _availableEvents = new Dictionary<Guid, IDataflowEvent>(keyValuePairs);
            _isCacheInvalid = false;

            return _availableEvents.Values;
        }
        finally
        {
            _cacheSemaphore.Release();
        }
    }

    private async Task<ClusterInfo?> LoadActiveClusterInfo()
    {
        var response = await _mediator.Request<GetClusterInfosRequest, GetClusterInfosResponse>(new GetClusterInfosRequest());
        if (response.RequestError is not null)
        {
            LogClusterRequestFailed(_logger, response.RequestError.ErrorCode, response.RequestError.Message);
            return null;
        }

        // for now we have only 1 cluster but at some point the cluster needs to be selectable
        return response.Results.FirstOrDefault(k => k.IsActive);
    }

    // retrieve events (connectors with EventEnabled=true)
    public async Task<IReadOnlyCollection<IDataflowEvent>> GetEvents(CancellationToken cancellationToken = default)
        => await UpdateEventCache(cancellationToken);

    public async Task<IAsyncDisposable> Subscribe(Guid eventId, Func<DateTime, string?, Task> handler, CancellationToken cancellationToken = default)
    {
        var result = await Subscribe(new Dictionary<Guid, Func<DateTime, string?, Task>> { [eventId] = handler }, cancellationToken);
        if (result.Failures.TryGetValue(eventId, out var failure))
            ExceptionDispatchInfo.Throw(failure);

        return result.Subscriptions[eventId];
    }

    public async Task<DataflowEventSubscriptions> Subscribe(IReadOnlyDictionary<Guid, Func<DateTime, string?, Task>> handlers, CancellationToken cancellationToken = default)
    {
        var availableEvents = await LoadAvailableEvents(cancellationToken);
        Dictionary<Guid, Exception> failures = [];
        var joined = Join(availableEvents, handlers, failures);
        await RequestTopics([.. joined.Where(s => s.IsFirst)], cancellationToken);
        var subscriptions = await AwaitTopics(joined, failures, cancellationToken);
        return new(subscriptions, failures);
    }

    private async Task<Dictionary<Guid, IDataflowEvent>> LoadAvailableEvents(CancellationToken cancellationToken)
    {
        _ = await UpdateEventCache(cancellationToken);
        return _availableEvents ?? throw new InvalidOperationException($"Failed to init events");
    }

    private List<Subscription> Join(Dictionary<Guid, IDataflowEvent> availableEvents,
        IReadOnlyDictionary<Guid, Func<DateTime, string?, Task>> handlers, Dictionary<Guid, Exception> failures)
    {
        List<Subscription> joined = [];
        foreach (var (eventId, handler) in handlers)
        {
            if (!availableEvents.TryGetValue(eventId, out var dataflowEvent))
            {
                failures[eventId] = new ArgumentException($"Event '{eventId}' does not exist");
                continue;
            }

            if (!dataflowEvent.CanRead)
            {
                failures[eventId] = new ArgumentException($"Event '{eventId}' is not readable");
                continue;
            }

            var runtimeEvent = _dataflowRuntimeEvents.GetOrAdd(eventId, _ => new DataflowRuntimeEvent { EventId = eventId });
            var subscription = new Subscription(this, dataflowEvent, runtimeEvent, handler);
            subscription.Join();
            joined.Add(subscription);
        }

        return joined;
    }

    // One request for the events that had no handler yet; handlers that join meanwhile wait for its outcome.
    private async Task RequestTopics(List<Subscription> firstSubscriptions, CancellationToken cancellationToken)
    {
        if (firstSubscriptions.Count == 0)
            return;

        try
        {
            await _mqttHandler.Subscribe([.. firstSubscriptions.Select(s => s.Event)], cancellationToken);
        }
        catch (Exception ex)
        {
            foreach (var subscription in firstSubscriptions)
                subscription.EndTopicRequest(ex);

            return;
        }

        foreach (var subscription in firstSubscriptions)
        {
            subscription.EndTopicRequest(null);
            LogSubscribedEvent(_logger, subscription.EventId);
        }
    }

    // A failed request ends its handlers, so a later attempt asks again. Once no handler is left, the topic is forgotten at
    // once, before another caller can subscribe it anew.
    private async Task<Dictionary<Guid, IAsyncDisposable>> AwaitTopics(List<Subscription> joined, Dictionary<Guid, Exception> failures,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, IAsyncDisposable> subscriptions = [];
        List<IDataflowEvent> cancelledEvents = [];
        foreach (var subscription in joined)
        {
            try
            {
                await subscription.TopicRequest.WaitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                failures[subscription.EventId] = ex;
                if (!subscription.End())
                    continue;

                _mqttHandler.Forget([subscription.Event]);
                if (ex is OperationCanceledException)
                    cancelledEvents.Add(subscription.Event);

                continue;
            }

            subscriptions[subscription.EventId] = subscription;
            if (!subscription.IsFirst)
                await SendLastValue(subscription);
        }

        await UnsubscribeCancelled(cancelledEvents);
        return subscriptions;
    }

    // A cancelled request may have reached the event broker, which would then keep sending values nobody reads.
    private async Task UnsubscribeCancelled(List<IDataflowEvent> cancelledEvents)
    {
        if (cancelledEvents.Count == 0)
            return;

        try
        {
            await _mqttHandler.Unsubscribe(cancelledEvents, CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogUnsubscribeCancelledFailed(_logger, ex, cancelledEvents.Count);
        }
    }

    // A handler that joins a subscribed event would otherwise wait for the next value, which may be long in coming.
    private async Task SendLastValue(Subscription subscription)
    {
        try
        {
            await subscription.SendLastValue();
        }
        catch (Exception ex)
        {
            LogReceivedEvent(_logger, ex, subscription.EventId);
        }
    }

    private async Task OnEventReceivedAsync(DataflowEventArgs arg)
    {
        if (!_dataflowRuntimeEvents.TryGetValue(arg.EventId, out var runtimeEvent))
        {
            LogMissingEventHandle(_logger, arg.EventId);
            return;
        }

        try
        {
            var payload = arg.Message.Payload;
            var payloadArray = System.Buffers.BuffersExtensions.ToArray(in payload);
            string? message;

            if (runtimeEvent.LastPayload.Span.SequenceEqual(payloadArray))
            {
                // Payload unchanged — reuse cached string, only update timestamp
                message = runtimeEvent.Value;
            }
            else
            {
                runtimeEvent.LastPayload = payloadArray;
                message = Encoding.UTF8.GetString(payloadArray);
                runtimeEvent.Value = message;
            }

            runtimeEvent.Timestamp = arg.Timestamp;

            var subs = runtimeEvent.Subscriptions;
            if (subs.IsEmpty) return;

            if (subs.Length == 1)
            {
                await subs[0](arg.Timestamp, message);
                return;
            }

            var tasks = new Task[subs.Length];
            for (var i = 0; i < subs.Length; i++)
                tasks[i] = subs[i](arg.Timestamp, message);
            await Task.WhenAll(tasks);
        }
        catch (ObjectDisposedException)
        {
            LogSubscriberDisposed(_logger, arg.EventId);
        }
        catch (Exception ex)
        {
            LogReceivedEvent(_logger, ex, arg.EventId);
        }
    }

    public async Task Unsubscribe(IReadOnlyCollection<IAsyncDisposable> subscriptions, CancellationToken cancellationToken = default)
    {
        var ownSubscriptions = subscriptions.OfType<Subscription>().Where(s => s.Broker == this).ToArray();
        if (ownSubscriptions.Length != subscriptions.Count)
            throw new ArgumentException("Only subscriptions of this broker can be ended together", nameof(subscriptions));

        List<IDataflowEvent> unusedEvents = [];
        foreach (var subscription in ownSubscriptions)
        {
            if (subscription.End())
                unusedEvents.Add(subscription.Event);
        }

        if (unusedEvents.Count == 0)
            return;

        await _mqttHandler.Unsubscribe(unusedEvents, cancellationToken);

        foreach (var dataflowEvent in unusedEvents)
            LogUnsubscribedEvent(_logger, dataflowEvent.EventId);
    }

    /// <returns><c>true</c> when <paramref name="handler"/> was the last one of its event, whose topic then is no longer needed.</returns>
    private bool RemoveHandler(DataflowRuntimeEvent runtimeEvent, Func<DateTime, string?, Task> handler)
    {
        if (!runtimeEvent.RemoveSubscription(handler))
            return false;

        _dataflowRuntimeEvents.TryRemove(KeyValuePair.Create(runtimeEvent.EventId, runtimeEvent));
        return true;
    }

    public async Task SetValue(Guid eventId, string? value, CancellationToken cancellationToken = default)
    {
        _ = await UpdateEventCache(cancellationToken);

        if (_availableEvents is null)
            throw new InvalidOperationException($"Failed to init events");

        if (!_availableEvents.TryGetValue(eventId, out var dataflowEvent))
            throw new ArgumentException($"Event '{eventId}' does not exist");

        if (!dataflowEvent.CanWrite)
            throw new ArgumentException($"Event '{eventId}' is not writable");

        LogSetValue(_logger, value, dataflowEvent.ValueType, eventId);

        await _mqttHandler.PublishValue(eventId, value, dataflowEvent.ValueType, cancellationToken);
    }

    public Task Consume(ClientContext<ClusterUpdateCompleted> context, CancellationToken cancellationToken)
    {
        _isCacheInvalid = true;
        _currentClusterInfo = null;

        var newCts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _reconnectCts, newCts);
        oldCts?.Cancel();
        oldCts?.Dispose();

        _reconnectTask = ReconnectAfterClusterUpdateAsync(newCts.Token);
        _ = NotifyEventsChanged();

        return Task.CompletedTask;
    }

    // The deployment event is handed to every consumer in turn, so a listener that reloads must not hold it up.
    // Each listener is awaited on its own; a multicast delegate would hand back the task of the last one only.
    private async Task NotifyEventsChanged()
    {
        if (EventsChanged is not { } eventsChanged)
            return;

        foreach (var listener in eventsChanged.GetInvocationList().Cast<Func<Task>>())
        {
            try
            {
                await listener();
            }
            catch (Exception ex)
            {
                LogEventsChangedFailed(_logger, ex);
            }
        }
    }

    private async Task ReconnectAfterClusterUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _mqttHandler.ResetAndReconnect(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down or new cluster update arrived
        }
        catch (Exception ex)
        {
            LogReconnectAfterClusterUpdateFailed(_logger, ex);
        }
    }

    /// <summary>
    /// One handler of one event. Ending it removes the handler once, however often it is ended. It keeps the event it was made
    /// for, which a reload of the events may have dropped by the time it ends.
    /// </summary>
    private sealed class Subscription(DataflowEventBroker broker, IDataflowEvent dataflowEvent, DataflowRuntimeEvent runtimeEvent,
        Func<DateTime, string?, Task> handler) : IAsyncDisposable
    {
        private int _isEnded;

        public DataflowEventBroker Broker => broker;

        public IDataflowEvent Event => dataflowEvent;

        public Guid EventId => runtimeEvent.EventId;

        /// <summary>
        /// The request for the event's topic this handler waits for; <see cref="IsFirst"/> when this handler has to send it.
        /// </summary>
        public Task TopicRequest { get; private set; } = Task.CompletedTask;

        public bool IsFirst { get; private set; }

        public void Join()
        {
            TopicRequest = runtimeEvent.AddSubscription(handler, out var isFirst);
            IsFirst = isFirst;
        }

        /// <returns><c>true</c> when this was the last handler of its event, whose topic then is no longer needed.</returns>
        public bool End()
            => Interlocked.Exchange(ref _isEnded, 1) == 0 && broker.RemoveHandler(runtimeEvent, handler);

        public void EndTopicRequest(Exception? failure)
            => runtimeEvent.EndTopicRequest(failure);

        public Task SendLastValue()
            => runtimeEvent.Timestamp is { } timestamp ? handler(timestamp, runtimeEvent.Value) : Task.CompletedTask;

        public ValueTask DisposeAsync()
            => new(broker.Unsubscribe([this]));
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Set value='{value}' type='{valueType}' on event '{EventId}'")]
    internal static partial void LogSetValue(ILogger logger, string? value, string valueType, Guid eventId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Subscribed to '{EventId}'")]
    internal static partial void LogSubscribedEvent(ILogger logger, Guid eventId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Unsubscribed from '{EventId}'")]
    internal static partial void LogUnsubscribedEvent(ILogger logger, Guid eventId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "No dataflow event handle registered for topic '{EventId}'")]
    internal static partial void LogMissingEventHandle(ILogger logger, Guid eventId);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Failed to update handle from topic '{EventId}'")]
    internal static partial void LogReceivedEvent(ILogger logger, Exception exception, Guid eventId);

    [LoggerMessage(EventId = 7, Level = LogLevel.Debug, Message = "Subscriber for topic '{EventId}' was disposed during event delivery")]
    internal static partial void LogSubscriberDisposed(ILogger logger, Guid eventId);

    [LoggerMessage(EventId = 8, Level = LogLevel.Warning, Message = "Request cluster infos failed with reason {Code}:{Reason}")]
    internal static partial void LogClusterRequestFailed(ILogger logger, int code, string? reason);

    [LoggerMessage(EventId = 9, Level = LogLevel.Error, Message = "Failed to reconnect after cluster update")]
    internal static partial void LogReconnectAfterClusterUpdateFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "Failed to unsubscribe {Count} events during dispose")]
    internal static partial void LogUnsubscribeDuringDisposeFailed(ILogger logger, Exception exception, int count);

    [LoggerMessage(EventId = 11, Level = LogLevel.Error, Message = "Failed to reload the events after a cluster update")]
    private static partial void LogEventsChangedFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 12, Level = LogLevel.Warning, Message = "Failed to unsubscribe {Count} events of a cancelled request")]
    private static partial void LogUnsubscribeCancelledFailed(ILogger logger, Exception exception, int count);
}
