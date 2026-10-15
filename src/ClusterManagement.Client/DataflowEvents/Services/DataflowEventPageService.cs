using ClusterManagement.Client.DataflowEvents.Models;
using ClusterManagement.Public.DataflowEvents;
using Microsoft.Extensions.Logging;
using Sdk.Client.Services;

namespace ClusterManagement.Client.DataflowEvents.Services;

internal partial class DataflowEventPageService(IDataflowEventBroker eventBroker, IMessageBannerService bannerService,
    TimeProvider timeProvider, ILogger<DataflowEventPageService> logger) : IAsyncDisposable
{
    private Dictionary<Guid, DataflowEventModel> _eventsById = [];
    private ShownEventSubscriptions? _shownSubscriptions;

    public List<DataflowEventModel> Events { get; private set; } = [];
    public Version? ActiveVersion { get; private set; }
    public Guid? ActiveClusterId { get; private set; }
    public string? ActiveClusterName { get; private set; }

    public event Func<Task>? RefreshRequested;

    /// <inheritdoc cref="IDataflowEventBroker.EventsChanged"/>
    public event Func<Task>? EventsChanged
    {
        add => eventBroker.EventsChanged += value;
        remove => eventBroker.EventsChanged -= value;
    }

    public async Task LoadDataflowEvents()
    {
        var dataflowEvents = await eventBroker.GetEvents();
        var clusterInfo = await eventBroker.GetCurrentClusterInfo();

        ActiveVersion = clusterInfo?.Version;
        ActiveClusterId = clusterInfo?.Id;
        ActiveClusterName = clusterInfo?.Name;

        // A reload creates new models; what was typed, written and received for an event stays.
        var previous = _eventsById;
        Events = [.. dataflowEvents.Select(k => CreateModel(k, previous.GetValueOrDefault(k.EventId)))];
        _eventsById = Events.ToDictionary(e => e.EventId);
    }

    private static DataflowEventModel CreateModel(IDataflowEvent dataflowEvent, DataflowEventModel? previous)
        => new()
        {
            CanRead = dataflowEvent.CanRead,
            CanWrite = dataflowEvent.CanWrite,
            EventId = dataflowEvent.EventId,
            Name = dataflowEvent.Name,
            ValueType = dataflowEvent.ValueType,
            FunctionBlockId = dataflowEvent.FunctionBlockId,
            FunctionBlockName = dataflowEvent.FunctionBlockName,
            ConnectorName = dataflowEvent.ConnectorName,
            DataflowName = dataflowEvent.DataflowName,
            ContainerPath = dataflowEvent.ContainerPath,
            PendingValue = previous?.PendingValue,
            Value = previous?.Value,
            Timestamp = previous?.Timestamp,
            IsValid = previous?.IsValid ?? true,
            EngineCycle = previous?.EngineCycle,
        };

    /// <returns><c>false</c> when nothing was written; a failure is shown as a banner.</returns>
    public async Task<bool> SetValue(DataflowEventModel? eventModel, string? value)
    {
        if (eventModel is null || string.IsNullOrEmpty(value))
            return false;

        try
        {
            await eventBroker.SetValue(eventModel.EventId, value);
            return true;
        }
        catch (Exception ex)
        {
            bannerService.ShowMessageBanner(Sdk.MessageBanner.Contracts.MessageType.Warning, ex.Message);
            LogSetValueError(logger, ex, value, eventModel.EventId);
            return false;
        }
    }

    /// <summary>
    /// The events the table shows now; their outputs are kept subscribed, see <see cref="ShownEventSubscriptions"/>.
    /// </summary>
    public void ShowEvents(IEnumerable<DataflowEventModel> shown)
        => (_shownSubscriptions ??= new(eventBroker, ValueHandlerFor, timeProvider, logger))
            .Show(shown.Where(e => e.CanRead).Select(e => e.EventId));

    private Func<DateTime, string?, Task> ValueHandlerFor(Guid eventId)
        => (timestamp, value) => OnValueChanged(eventId, timestamp, value);

    // A reload replaces the models; a value goes to the current one of its event.
    private async Task OnValueChanged(Guid eventId, DateTime timestamp, string? value)
    {
        if (!_eventsById.TryGetValue(eventId, out var model))
            return;

        var details = eventBroker.DetailsOf(eventId);
        model.Timestamp = timestamp;
        model.Value = value;
        model.IsValid = details?.IsValid ?? true;
        model.EngineCycle = details?.EngineCycle;
        if (RefreshRequested is not null)
            await RefreshRequested.Invoke();
    }

    // The page disposes it when it closes, the circuit's scope once more at its end; a page opened again in between
    // starts over.
    public async ValueTask DisposeAsync()
    {
        if (_shownSubscriptions is null)
            return;

        await _shownSubscriptions.DisposeAsync();
        _shownSubscriptions = null;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to set value '{Value}' on {EventId}")]
    private static partial void LogSetValueError(ILogger<DataflowEventPageService> logger, Exception exception, string value, Guid eventId);
}
