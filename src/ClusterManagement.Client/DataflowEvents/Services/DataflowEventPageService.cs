using ClusterManagement.Client.DataflowEvents.Models;
using ClusterManagement.Public.DataflowEvents;
using Microsoft.Extensions.Logging;
using Sdk.Client.Services;

namespace ClusterManagement.Client.DataflowEvents.Services;

internal partial class DataflowEventPageService(IDataflowEventBroker eventBroker, IMessageBannerService bannerService, ILogger<DataflowEventPageService> logger) : IAsyncDisposable
{
    private Dictionary<Guid, DataflowEventModel> _eventsById = [];

    public List<DataflowEventModel> Events { get; private set; } = [];
    public Version? ActiveVersion { get; private set; }
    public Guid? ActiveClusterId { get; private set; }

    public event Func<Task>? RefreshRequested;

    public async Task LoadDataflowEvents()
    {
        var dataflowEvents = await eventBroker.GetEvents();
        var clusterInfo = await eventBroker.GetCurrentClusterInfo();

        ActiveVersion = clusterInfo?.Version;
        ActiveClusterId = clusterInfo?.Id;

        // A reload creates new models; what was typed, written, received and subscribed for an event stays.
        var previous = _eventsById;
        Events = [.. dataflowEvents.Select(k => CreateModel(k, previous.GetValueOrDefault(k.EventId)))];
        _eventsById = Events.ToDictionary(e => e.EventId);

        foreach (var removed in previous.Values.Where(e => !_eventsById.ContainsKey(e.EventId)))
            await RemoveSubscription(removed);
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
            Subscription = previous?.Subscription,
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

    private async Task AddSubscription(DataflowEventModel eventModel)
    {
        if (eventModel.Subscription is not null)
            return;

        eventModel.Subscription = await eventBroker.Subscribe(eventModel.EventId, (t, v) => OnValueChanged(eventModel, t, v));
    }

    private static async Task RemoveSubscription(DataflowEventModel eventModel)
    {
        if (eventModel.Subscription is null)
            return;

        await eventModel.Subscription.DisposeAsync();
        eventModel.Subscription = null;
    }

    public async Task SubscribeAll()
    {
        try
        {
            foreach (var eventModel in Events.Where(k => k.Subscription is null && k.CanRead).ToArray())
            {
                await AddSubscription(eventModel);
            }
        }
        catch (Exception ex)
        {
            LogSubscribeAllError(logger, ex);
        }
    }

    public async Task SubscribeEvent(DataflowEventModel eventModel)
    {
        try
        {
            await AddSubscription(eventModel);
        }
        catch (Exception ex)
        {
            bannerService.ShowMessageBanner(Sdk.MessageBanner.Contracts.MessageType.Warning, ex.Message);
            LogSubscribeEventError(logger, ex, eventModel.Name);
        }
    }

    public async Task UnsubscribeAll()
    {
        try
        {
            foreach (var eventModel in Events.Where(k => k.Subscription is not null).ToArray())
            {
                await RemoveSubscription(eventModel);
            }
        }
        catch (Exception ex)
        {
            LogUnsubscribeAllError(logger, ex);
        }
    }

    public async Task UnsubscribeEvent(DataflowEventModel eventModel)
    {
        try
        {
            await RemoveSubscription(eventModel);
        }
        catch (Exception ex)
        {
            bannerService.ShowMessageBanner(Sdk.MessageBanner.Contracts.MessageType.Warning, ex.Message);
            LogUnsubscribeEventError(logger, ex, eventModel.Name);
        }
    }

    // A subscription outlives the model it was made for when the events are reloaded; the value goes to the current one.
    private async Task OnValueChanged(DataflowEventModel subscribedModel, DateTime timestamp, string? value)
    {
        var model = _eventsById.GetValueOrDefault(subscribedModel.EventId, subscribedModel);
        model.Timestamp = timestamp;
        model.Value = value;
        if (RefreshRequested is not null)
            await RefreshRequested.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var item in Events)
        {
            if (item.Subscription is null)
                continue;

            await item.Subscription.DisposeAsync();
            item.Subscription = null;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to set value '{Value}' on {EventId}")]
    private static partial void LogSetValueError(ILogger<DataflowEventPageService> logger, Exception exception, string value, Guid eventId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to subscribe all events")]
    private static partial void LogSubscribeAllError(ILogger<DataflowEventPageService> logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to subscribe event {EventName}")]
    private static partial void LogSubscribeEventError(ILogger<DataflowEventPageService> logger, Exception exception, string eventName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to unsubscribe all events")]
    private static partial void LogUnsubscribeAllError(ILogger<DataflowEventPageService> logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to unsubscribe event {EventName}")]
    private static partial void LogUnsubscribeEventError(ILogger<DataflowEventPageService> logger, Exception exception, string eventName);
}
