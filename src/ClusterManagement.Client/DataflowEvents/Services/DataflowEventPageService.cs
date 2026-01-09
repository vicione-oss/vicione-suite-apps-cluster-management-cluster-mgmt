using ClusterManagement.Client.DataflowEvents.Models;
using Microsoft.Extensions.Logging;
using Sdk.Client.Services;

namespace ClusterManagement.Client.DataflowEvents.Services;

internal class DataflowEventPageService(IDataflowEventBroker eventBroker, IMessageBannerService bannerService, ILogger<DataflowEventPageService> logger) : IAsyncDisposable
{
    private readonly IDataflowEventBroker _eventBroker = eventBroker;
    private readonly IMessageBannerService _bannerService = bannerService;
    private readonly ILogger<DataflowEventPageService> _logger = logger;

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
        var dataflowEvents = await _eventBroker.GetEvents();
        var clusterInfo = await _eventBroker.GetCurrentClusterInfo();

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
    }

    public Task SetSelectedValue() => SetValue(SelectedEvent, SendInputValue);

    public async Task SetValue(DataflowEventModel? eventModel, string? value)
    {
        if (eventModel is null || string.IsNullOrEmpty(value))
            return;

        try
        {
            await _eventBroker.SetValue(eventModel.EventId, value);
        }
        catch (Exception ex)
        {
            _bannerService.ShowMessageBanner(Sdk.MessageBanner.Contracts.MessageType.Warning, ex.Message);
            _logger.LogError(ex, "Failed to set value '{Value}' on {EventId}", value, eventModel.EventId);
        }
    }

    private async Task AddSubscription(DataflowEventModel eventModel)
    {
        if (eventModel.Subscription is not null)
            return;

        eventModel.Subscription = await _eventBroker.Subscribe(eventModel.EventId, (t, v) => OnValueChanged(eventModel, t, v));
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
            _logger.LogError(ex, "Failed to subscribe all events");
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
            _bannerService.ShowMessageBanner(Sdk.MessageBanner.Contracts.MessageType.Warning, ex.Message);
            _logger.LogError(ex, "Failed to subscribe event {Event}", eventModel.Name);
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
            _logger.LogError(ex, "Failed to unsubscribe all events");
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
            _bannerService.ShowMessageBanner(Sdk.MessageBanner.Contracts.MessageType.Warning, ex.Message);
            _logger.LogError(ex, "Failed to unsubscribe event {Event}", eventModel.Name);
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
}
