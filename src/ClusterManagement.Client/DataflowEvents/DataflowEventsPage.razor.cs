using System.Globalization;
using ClusterManagement.Client.DataflowEvents.Models;
using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Client.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ViciOne.Ui.Blazor.Components.Button.Enums;
using ViciOne.Ui.Blazor.Components.Button.Extensions;
using ViciOne.Ui.ColorableIcons;
using ViciOne.Ui.Localization.Resources;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace ClusterManagement.Client.DataflowEvents;

public sealed partial class DataflowEventsPage
{
    // Up to this many events the groups start expanded; larger dataflows start with the overview of their groups.
    private const int ExpandedEventLimit = 60;
    private const int ConnectorIconSize = 24;
    private const string LegendColor = "rgb(140, 140, 140)";

    private static readonly TimeSpan s_copiedDuration = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan s_renderDelay = TimeSpan.FromMilliseconds(250);
    private static readonly MarkupString s_legendInputIcon = new(ColoredIconFactory.GetConnectorIcon(LegendColor, true, ConnectorIconSize));
    private static readonly MarkupString s_legendOutputIcon = new(ColoredIconFactory.GetConnectorIcon(LegendColor, false, ConnectorIconSize));

    private readonly string _plusIconCssClass = MonochromeIconName.PlusSlim.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _minusIconCssClass = MonochromeIconName.MinusSlim.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _refreshIconCssClass = MonochromeIconName.Refresh.GetCssClasses(ButtonSize.Medium.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _sendIconCssClass = MonochromeIconName.SendLight.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _copyIconCssClass = MonochromeIconName.Duplicate.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _expandIconCssClass = MonochromeIconName.ExpanderLightRight.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _collapseIconCssClass = MonochromeIconName.ExpanderLightDown.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly CancellationTokenSource _tokenSource = new();

    // Groups the user opened or closed against the current default.
    private readonly HashSet<string> _toggledGroups = [];

    // Failed writes per Boolean input; a new key recreates its checkbox, which keeps the clicked state in the browser.
    private readonly Dictionary<Guid, int> _failedWrites = [];

    private IReadOnlyList<DataflowEventGroup> _groups = [];
    private List<TableRow> _rows = [];
    private int _isRenderScheduled;
    private DataflowEventGrouping _grouping = DataflowEventGrouping.FunctionBlock;
    private DataflowEventDirection _direction = DataflowEventDirection.All;
    private bool _expandByDefault;
    private bool _expandFilteredByDefault = true;
    private string? _filter;
    private Guid? _copiedEventId;

    [Inject]
    private DataflowEventPageService PageService { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)]
    private TimeProvider TimeProvider { get; set; } = default!;

    protected override string PageTitle => Localization.DataflowEventsPage.PageTitle;

    private static IEnumerable<(DataflowEventGrouping Grouping, string Label)> Groupings => [
        (DataflowEventGrouping.None, Localization.DataflowEventsPage.GroupByNone),
        (DataflowEventGrouping.Dataflow, Localization.DataflowEventsPage.GroupByDataflow),
        (DataflowEventGrouping.Container, Localization.DataflowEventsPage.GroupByContainer),
        (DataflowEventGrouping.FunctionBlock, Localization.DataflowEventsPage.GroupByFunctionBlock),
        (DataflowEventGrouping.Direction, Localization.DataflowEventsPage.GroupByDirection),
        (DataflowEventGrouping.ValueType, Localization.DataflowEventsPage.GroupByValueType),
    ];

    private static IEnumerable<(DataflowEventDirection Direction, string Label)> Directions => [
        (DataflowEventDirection.All, CommonVocabulary.All),
        (DataflowEventDirection.Inputs, Localization.DataflowEventsPage.Inputs),
        (DataflowEventDirection.Outputs, Localization.DataflowEventsPage.Outputs),
    ];

    private bool IsSubscribed => PageService.Events.Any(e => e.Subscription is not null);

    private int ShownCount => _groups.Sum(g => g.Events.Count);

    private bool IsAnyGroupExpanded => _groups.Any(IsExpanded);

    private string ToggleSubscriptionsText
        => string.Format(CultureInfo.CurrentCulture, IsSubscribed ? UserActions.UnsubscribeSomething : UserActions.SubscribeSomething, CommonVocabulary.All);

    private string ToggleExpansionText
        => string.Format(CultureInfo.CurrentCulture, IsAnyGroupExpanded ? UserActions.CollapseSomething : UserActions.ExpandSomething, CommonVocabulary.All);

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await PageService.LoadDataflowEvents();
        _expandByDefault = PageService.Events.Count <= ExpandedEventLimit;
        UpdateGroups();
        PageService.RefreshRequested += OnRefreshRequested;
    }

    private async Task RefreshDataflowEvents()
    {
        await PageService.LoadDataflowEvents();
        UpdateGroups();

        await InvokeAsync(StateHasChanged);
    }

    private Task ToggleSubscriptions()
        => IsSubscribed ? PageService.UnsubscribeAll() : PageService.SubscribeAll();

    // A new filter opens every group it found; the groups can then be closed again.
    private void ApplyFilter(string? filter)
    {
        _filter = filter;
        _expandFilteredByDefault = true;
        _toggledGroups.Clear();
        UpdateGroups();
    }

    private void SetGrouping(DataflowEventGrouping grouping)
    {
        _grouping = grouping;
        _toggledGroups.Clear();
        UpdateGroups();
    }

    private void SetDirection(DataflowEventDirection direction)
    {
        _direction = direction;
        UpdateGroups();
    }

    private void UpdateGroups()
    {
        _groups = DataflowEventGroups.Build(PageService.Events, _grouping, _direction, _filter);
        UpdateRows();
    }

    // The table renders only the rows in view, so it lists the headings and the events of expanded groups as one list.
    private void UpdateRows()
    {
        List<TableRow> rows = [];
        foreach (var group in _groups)
        {
            if (_grouping != DataflowEventGrouping.None)
                rows.Add(new(group, null));

            if (IsExpanded(group))
                rows.AddRange(group.Events.Select(dataflowEvent => new TableRow(group, dataflowEvent)));
        }

        _rows = rows;
    }

    // Without grouping there is nothing to collapse.
    private bool IsExpanded(DataflowEventGroup group)
        => _grouping == DataflowEventGrouping.None || ExpandsByDefault != _toggledGroups.Contains(group.Key);

    private bool ExpandsByDefault => IsFiltered ? _expandFilteredByDefault : _expandByDefault;

    private bool IsFiltered => !string.IsNullOrWhiteSpace(_filter);

    private void ToggleGroup(DataflowEventGroup group)
    {
        if (!_toggledGroups.Add(group.Key))
            _toggledGroups.Remove(group.Key);

        UpdateRows();
    }

    private void ToggleExpansion()
    {
        var expand = !IsAnyGroupExpanded;
        if (IsFiltered)
            _expandFilteredByDefault = expand;
        else
            _expandByDefault = expand;

        _toggledGroups.Clear();
        UpdateRows();
    }

    private async Task CopyValue(DataflowEventModel dataflowEvent)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", _tokenSource.Token, dataflowEvent.Value);
        }
        catch (JSException)
        {
            // The browser refused the clipboard, for example without a secure context; there is nothing to confirm.
            return;
        }

        _copiedEventId = dataflowEvent.EventId;
        _ = ClearCopiedAfterDelay(dataflowEvent.EventId, _tokenSource.Token);
    }

    private async Task ClearCopiedAfterDelay(Guid eventId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(s_copiedDuration, TimeProvider, cancellationToken);
            await InvokeAsync(() =>
            {
                if (_copiedEventId == eventId)
                    _copiedEventId = null;

                StateHasChanged();
            });
        }
        catch (OperationCanceledException)
        {
            // The page was closed.
        }
    }

    private static string ChipCssClass(bool isActive)
        => isActive ? "chip-toggle active" : "chip-toggle";

    // Containers lead, so function blocks of the same name in different containers stay apart at a glance.
    private string GroupLabel(DataflowEventGroup group)
    {
        var first = group.Events[0];
        return _grouping switch
        {
            DataflowEventGrouping.Dataflow => first.DataflowName,
            DataflowEventGrouping.Container => first.Location,
            DataflowEventGrouping.FunctionBlock => $"{first.Location} › {first.FunctionBlockName}",
            DataflowEventGrouping.Direction => first.CanWrite ? Localization.DataflowEventsPage.InputsGroup : Localization.DataflowEventsPage.OutputsGroup,
            DataflowEventGrouping.ValueType => ValueTypeNames.Short(first.ValueType),
            _ => Localization.DataflowEventsPage.AllEvents,
        };
    }

    private static bool IsBoolean(DataflowEventModel dataflowEvent)
        => ValueTypeNames.Is(dataflowEvent.ValueType, typeof(bool));

    // Inputs cannot be read back, so the checkbox shows the value last written from this page.
    private async Task WriteBoolean(DataflowEventModel dataflowEvent, bool value)
    {
        var text = value ? bool.TrueString : bool.FalseString;
        if (await PageService.SetValue(dataflowEvent, text))
            dataflowEvent.PendingValue = text;
        else
            _failedWrites[dataflowEvent.EventId] = _failedWrites.GetValueOrDefault(dataflowEvent.EventId) + 1;
    }

    private (Guid, int) CheckBoxKey(DataflowEventModel dataflowEvent)
        => (dataflowEvent.EventId, _failedWrites.GetValueOrDefault(dataflowEvent.EventId));

    private Task WritePending(DataflowEventModel dataflowEvent)
        => PageService.SetValue(dataflowEvent, dataflowEvent.PendingValue);

    private static MarkupString ConnectorIcon(DataflowEventModel dataflowEvent)
        => new(ColoredIconFactory.GetConnectorIcon(ConnectorColors.For(dataflowEvent.ValueType), dataflowEvent.CanWrite, ConnectorIconSize));

    private static string DirectionTitle(DataflowEventModel dataflowEvent)
        => string.Format(CultureInfo.CurrentCulture,
            dataflowEvent.CanWrite ? Localization.DataflowEventsPage.InputTitle : Localization.DataflowEventsPage.OutputTitle,
            ValueTypeNames.Short(dataflowEvent.ValueType));

    // A value without a timestamp arrives with DateTime.MinValue; it shows no time instead of midnight.
    // The date shows only for a value from an earlier day, so today's values stay short.
    private string TimeText(DateTime? timestamp)
    {
        if (timestamp is not { } value || value == default)
            return string.Empty;

        var zone = TimeProvider.LocalTimeZone;
        var received = AsUtc(value);
        var isToday = TimeZoneInfo.ConvertTime(received, zone).Date == TimeZoneInfo.ConvertTime(TimeProvider.GetUtcNow(), zone).Date;
        return isToday ? received.To24HourTime(zone) : received.ToShortDateAnd24HourTime(zone);
    }

    private string? TimeTitle(DateTime? timestamp)
        => timestamp is { } value && value != default
            ? $"{AsUtc(value).ToShortDateAnd24HourTime(TimeProvider.LocalTimeZone)} ({TimeProvider.LocalTimeZone.Id})"
            : null;

    // Event timestamps are UTC; one sent without a zone would otherwise be read as the server's local time.
    private static DateTimeOffset AsUtc(DateTime timestamp)
        => new(timestamp.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(timestamp, DateTimeKind.Utc) : timestamp);

    private static string ValueInputId(DataflowEventModel dataflowEvent)
        => $"value-{dataflowEvent.EventId}";

    // Values of many subscribed events arrive close together; they are rendered together instead of one render each.
    private Task OnRefreshRequested()
    {
        if (Interlocked.Exchange(ref _isRenderScheduled, 1) == 0)
            _ = RenderAfterDelay(_tokenSource.Token);

        return Task.CompletedTask;
    }

    private async Task RenderAfterDelay(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(s_renderDelay, TimeProvider, cancellationToken);
            await InvokeAsync(() =>
            {
                Volatile.Write(ref _isRenderScheduled, 0);
                StateHasChanged();
            });
        }
        catch (OperationCanceledException)
        {
            // The page was closed.
        }
    }

    /// <summary>
    /// One row of the table: the heading of <see cref="Group"/> without an <see cref="Event"/>, or one of its events.
    /// </summary>
    private readonly record struct TableRow(DataflowEventGroup Group, DataflowEventModel? Event);

    protected override async ValueTask DisposeInternal()
    {
        try
        {
            await _tokenSource.CancelAsync();
            _tokenSource.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Ignore: CancellationTokenSource was already disposed.
        }

        if (PageService != null)
        {
            PageService.RefreshRequested -= OnRefreshRequested;
            await PageService.DisposeAsync();
        }

        await base.DisposeInternal();
    }
}
