using System.Collections.Concurrent;
using System.Text;
using ClusterManagement.Client.DataflowEvents.Models;
using ClusterManagement.Client.Extensions;
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

        if (_availableEvents is not null)
        {
            foreach (var runtimeEvent in _dataflowRuntimeEvents.Values)
            {
                if (!_availableEvents.TryGetValue(runtimeEvent.EventId, out var dataflowEvent))
                    continue;

                try
                {
                    await _mqttHandler.Unsubscribe(dataflowEvent);
                }
                catch (Exception ex)
                {
                    LogUnsubscribeDuringDisposeFailed(_logger, ex, dataflowEvent.EventId);
                }
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
        _ = await UpdateEventCache(cancellationToken);

        if (_availableEvents is null)
            throw new InvalidOperationException($"Failed to init events");

        if (!_availableEvents.TryGetValue(eventId, out var dataflowEvent))
            throw new ArgumentException($"Event '{eventId}' does not exist");

        if (!dataflowEvent.CanRead)
            throw new ArgumentException($"Event '{eventId}' is not readable");

        var runtimeEvent = _dataflowRuntimeEvents.GetOrAdd(eventId, _ => new DataflowRuntimeEvent { EventId = eventId });
        var count = runtimeEvent.AddSubscription(handler);

        if (count == 1)
        {
            await _mqttHandler.Subscribe(dataflowEvent, cancellationToken);

            LogSubscribedEvent(_logger, dataflowEvent.EventId);
        }
        else if (runtimeEvent.Timestamp.HasValue)
        {
            await handler(runtimeEvent.Timestamp.Value, runtimeEvent.Value);
        }

        return AsyncDisposable.Create((runtimeEvent, handler), Unsubscribe);
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

    private async Task Unsubscribe((DataflowRuntimeEvent, Func<DateTime, string?, Task>) subscription)
    {
        var (runtimeEvent, handler) = subscription;
        var wasLast = runtimeEvent.RemoveSubscription(handler);

        if (wasLast)
        {
            if (_availableEvents?.TryGetValue(runtimeEvent.EventId, out var dataflowEvent) == true)
            {
                await _mqttHandler.Unsubscribe(dataflowEvent);

                LogUnsubscribedEvent(_logger, dataflowEvent.EventId);
            }

            _dataflowRuntimeEvents.TryRemove(runtimeEvent.EventId, out _);
        }
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

        return Task.CompletedTask;
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

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "Failed to unsubscribe event '{EventId}' during dispose")]
    internal static partial void LogUnsubscribeDuringDisposeFailed(ILogger logger, Exception exception, Guid eventId);
}
