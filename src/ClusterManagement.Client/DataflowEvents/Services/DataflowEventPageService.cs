using ClusterManagement.Client.DataflowEvents.Models;
using Microsoft.Extensions.Logging;
using Sdk.Client.Services;

namespace ClusterManagement.Client.DataflowEvents.Services;

internal partial class DataflowEventPageService(IDataflowEventBroker eventBroker, IMessageBannerService bannerService, ILogger<DataflowEventPageService> logger) : IAsyncDisposable
{
    public List<DataflowEventModel> Events { get; private set; } = [];
    public DataflowEventModel? SelectedEvent { get; set; }
    public string? ActiveVersion { get; private set; }
    public Guid? ActiveClusterId { get; private set; }
    public string? SendInputValue { get; set; }
    public bool CanSetValue => SelectedEvent is not null
        && SelectedEvent.CanWrite
        && !string.IsNullOrEmpty(SendInputValue);

    public event Func<Task>? RefreshRequested;

    public async Task LoadDataflowEvents()
    {
        var dataflowEvents = await eventBroker.GetEvents();
        var clusterInfo = await eventBroker.GetCurrentClusterInfo();

        ActiveVersion = $"v{clusterInfo?.Version}";
        ActiveClusterId = clusterInfo?.Id;

        Events = [.. dataflowEvents.Select(k => new DataflowEventModel
        {
            CanRead = k.CanRead,
            CanWrite = k.CanWrite,
            EventId = k.EventId,
            Name = k.Name,
            ValueType = k.ValueType,
        })];

        // keep the selection across reloads; the reload creates new model instances
        SelectedEvent = Events.FirstOrDefault(e => e.EventId == SelectedEvent?.EventId);
    }

    public Task SetSelectedValue() => SetValue(SelectedEvent, SendInputValue);

    public async Task SetValue(DataflowEventModel? eventModel, string? value)
    {
        if (eventModel is null || string.IsNullOrEmpty(value))
            return;

        try
        {
            await eventBroker.SetValue(eventModel.EventId, value);
        }
        catch (Exception ex)
        {
            bannerService.ShowMessageBanner(Sdk.MessageBanner.Contracts.MessageType.Warning, ex.Message);
            LogSetValueError(logger, ex, value, eventModel.EventId);
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
            return; // should not happen

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

    private async Task OnValueChanged(DataflowEventModel model, DateTime timestamp, string? value)
    {
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
