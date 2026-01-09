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
    private Dictionary<Guid, IDataflowEvent>? _availableEvents;
    private ClusterInfo? _currentClusterInfo;
    private readonly ConcurrentDictionary<Guid, DataflowRuntimeEvent> _dataflowRuntimeEvents = [];
    private readonly AutoDisposeList<IDisposable> _mediatorHandles = [];
    private bool _isCacheInvalid;

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

        _mqttHandler.EventReceivedAsync -= OnEventReceivedAsync;

        if (_availableEvents is not null)
        {
            foreach (var availableEvent in _availableEvents.Values)
                await _mqttHandler.Unsubscribe(availableEvent);
        }
        _dataflowRuntimeEvents.Clear();

        await _mqttHandler.Disconnect();
    }

    public async Task<ClusterInfo?> GetCurrentClusterInfo()
        => _currentClusterInfo ??= await LoadActiveClusterInfo();

    private async Task<IEnumerable<IDataflowEvent>> UpdateEventCache()
    {
        // todo check cluster id + version when deployment changed
        if (_availableEvents is not null && !_isCacheInvalid)
            return _availableEvents.Values;

        var clusterInfo = await GetCurrentClusterInfo();
        if (clusterInfo is null)
            return [];

        // later on we could select events related to a dataflow
        var request = new GetDataflowEventsRequest { ClusterId = clusterInfo.Id };
        var response = await _mediator.Request<GetDataflowEventsRequest, GetDataflowEventsResponse>(request);
        if (response.RequestError is not null)
            return [];

        var keyValuePairs = response.Results
            .OrderBy(k => k.Name)
            .Select(k => new KeyValuePair<Guid, IDataflowEvent>(k.EventId, k));

        _availableEvents = new Dictionary<Guid, IDataflowEvent>(keyValuePairs);
        _isCacheInvalid = false;

        return _availableEvents.Values;
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

    public async Task<IReadOnlyCollection<IDataflowEvent>> GetEvents()
    {
        // retrieve events (connectors with EventEnabled=true)
        var clusterEvents = await UpdateEventCache();

        return clusterEvents.ToList().AsReadOnly();
    }

    public async Task<IAsyncDisposable> Subscribe(Guid eventId, Func<DateTime, string?, Task> handler)
    {
        _ = await UpdateEventCache();

        if (_availableEvents is null)
            throw new InvalidOperationException($"Failed to init events");

        if (!_availableEvents.TryGetValue(eventId, out var dataflowEvent))
            throw new ArgumentException($"Event '{eventId}' does not exist");

        if (!dataflowEvent.CanRead)
            throw new ArgumentException($"Event '{eventId}' is not readable");

        var dataflowRuntimeEvent = _dataflowRuntimeEvents.GetOrAdd(eventId, new DataflowRuntimeEvent() { EventId = eventId, });
        dataflowRuntimeEvent.Subscriptions.Add(handler);

        // only subscribe the topic if we have not done it for another handle already
        if (dataflowRuntimeEvent.Subscriptions.Count == 1)
        {
            await _mqttHandler.Subscribe(dataflowEvent);

            LogSubscribedEvent(_logger, dataflowEvent.EventId);
        }
        else if (dataflowRuntimeEvent.Timestamp.HasValue)
        {
            await handler(dataflowRuntimeEvent.Timestamp.Value, dataflowRuntimeEvent.Value);
        }

        return AsyncDisposable.Create((dataflowRuntimeEvent, handler), Unsubscribe);
    }

    private async Task OnEventReceivedAsync(DataflowEventArgs arg)
    {
        if (_dataflowRuntimeEvents.TryGetValue(arg.EventId, out var dataflowRuntimeEvent))
        {
            dataflowRuntimeEvent.Timestamp = arg.Timestamp;
            try
            {
                var message = Encoding.UTF8.GetString(arg.Message.Payload);
                dataflowRuntimeEvent.Value = message;
                await Task.WhenAll([.. dataflowRuntimeEvent.Subscriptions.Select(h => h(arg.Timestamp, message))]);
            }
            catch (Exception ex)
            {
                LogReceivedEvent(_logger, ex, arg.EventId);
            }
        }
        else
        {
            LogMissingEventHandle(_logger, arg.EventId);
        }
    }

    private async Task Unsubscribe((DataflowRuntimeEvent, Func<DateTime, string?, Task>) subscription)
    {
        var (dataflowRuntimeEvent, handler) = subscription;
        dataflowRuntimeEvent.Subscriptions.Remove(handler);

        // only unsubscribe if we remove the last subscription for the topic
        if (dataflowRuntimeEvent.Subscriptions.Count == 0)
        {
            if (_availableEvents?.TryGetValue(dataflowRuntimeEvent.EventId, out var dataflowEvent) == true)
            {
                await _mqttHandler.Unsubscribe(dataflowEvent);

                LogUnsubscribedEvent(_logger, dataflowEvent.EventId);
            }
            _dataflowRuntimeEvents.TryRemove(dataflowRuntimeEvent.EventId, out _);
        }
    }

    public async Task SetValue(Guid eventId, string? value, CancellationToken cancellationToken = default)
    {
        _ = await UpdateEventCache();

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
        // a new cluster got deployed - invalidate cache
        if (_currentClusterInfo is null)
        {
            _isCacheInvalid = true;
            return Task.CompletedTask;
        }

        // trigger refresh cache
        _isCacheInvalid = _currentClusterInfo.Version != context.Message.NewVersion;
        _currentClusterInfo = null;
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Set value='{value}' type='{valueType}' on event '{EventId}'")]
    internal static partial void LogSetValue(ILogger logger, string? value, string valueType, Guid eventId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Subscribed to '{eventId}'")]
    internal static partial void LogSubscribedEvent(ILogger logger, Guid eventId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Unsubscribed from '{eventId}'")]
    internal static partial void LogUnsubscribedEvent(ILogger logger, Guid eventId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "No dataflow event handle registered for topic {eventId}")]
    internal static partial void LogMissingEventHandle(ILogger logger, Guid eventId);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Failed to update handle from topic {eventId}")]
    internal static partial void LogReceivedEvent(ILogger logger, Exception exception, Guid eventId);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "Request cluster infos failed with reason {Code}:{Reason}")]
    internal static partial void LogClusterRequestFailed(ILogger logger, int code, string? reason);
}
