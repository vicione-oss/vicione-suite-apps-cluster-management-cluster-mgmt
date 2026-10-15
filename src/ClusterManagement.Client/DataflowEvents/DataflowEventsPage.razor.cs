using System.Globalization;
using ClusterManagement.Client.DataflowEvents.Models;
using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.Management.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;
using ViciOne.Ui.Blazor.Components.Button.Enums;
using ViciOne.Ui.Blazor.Components.Button.Extensions;
using ViciOne.Ui.ColorableIcons;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace ClusterManagement.Client.DataflowEvents;

public sealed partial class DataflowEventsPage
{
    // Up to this many events the flat groups start expanded; larger dataflows start with the overview of their groups.
    // The container tree always starts with its dataflows only.
    private const int ExpandedEventLimit = 60;
    private const int ConnectorIconSize = 24;
    private const string TreeKeysModule = "./_content/ViciOne.Suite.ClusterManagement.Client/DataflowEvents/DataflowEventsPage.razor.js";

    private static readonly TimeSpan s_copiedDuration = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan s_renderDelay = TimeSpan.FromMilliseconds(250);

    private readonly string _sendIconCssClass = MonochromeIconName.SendLight.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _copyIconCssClass = MonochromeIconName.Duplicate.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _expandIconCssClass = MonochromeIconName.ExpanderLightRight.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _collapseIconCssClass = MonochromeIconName.ExpanderLightDown.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly CancellationTokenSource _tokenSource = new();

    // Groups the user opened or closed against the current default.
    private readonly HashSet<string> _toggledGroups = [];

    // Failed writes per Boolean input; a new key recreates its checkbox, which keeps the clicked state in the browser.
    private readonly Dictionary<Guid, int> _failedWrites = [];

    private IReadOnlyList<DataflowEventNode> _nodes = [];
    private List<TableRow> _rows = [];
    private Virtualize<TableRow>? _virtualize;
    private ElementReference _table;
    private IJSObjectReference? _treeKeys;
    private int _isRenderScheduled;
    private DataflowEventGrouping _grouping = DataflowEventGrouping.Container;
    private DataflowEventDirection _direction = DataflowEventDirection.All;
    private bool _expandByDefault;
    private bool _expandFilteredByDefault = true;
    private bool _isLoading = true;
    private string? _filter;
    private Guid? _copiedEventId;

    [Inject]
    private DataflowEventPageService PageService { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)]
    private TimeProvider TimeProvider { get; set; } = default!;

    [Inject]
    private ExpertViewState ExpertView { get; set; } = default!;

    private bool Expert => ExpertView.IsExpert;

    private int ColumnCount => Expert ? 6 : 5;

    // The top bar keeps the app's name; the page bar says which page of it this is.
    protected override string PageTitle => Management.Localization.Common.ModuleTitle;

    private int ShownCount => _nodes.Sum(n => n.Group.Events.Count);

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await PageService.LoadDataflowEvents();
        _isLoading = false;
        _expandByDefault = OpensExpanded(_grouping);
        UpdateGroups();
        PageService.RefreshRequested += OnRefreshRequested;
        PageService.EventsChanged += OnEventsChanged;
        ExpertView.Changed += OnExpertViewChanged;
    }

    protected override async Task OnAfterRenderedAsync(bool firstRender)
    {
        await base.OnAfterRenderedAsync(firstRender);
        if (!firstRender)
            return;

        var treeKeys = await JSRuntime.InvokeAsync<IJSObjectReference>("import", _tokenSource.Token, TreeKeysModule);

        // A page left while the module loaded is already disposed, so nothing would dispose the module later.
        if (_tokenSource.IsCancellationRequested)
        {
            await treeKeys.DisposeAsync();
            return;
        }

        _treeKeys = treeKeys;
        await _treeKeys.InvokeVoidAsync("attach", _tokenSource.Token, _table);
    }

    // A deployment changes the events of the running version; the page shows them without a refresh.
    private async Task OnEventsChanged()
    {
        try
        {
            await InvokeAsync(async () =>
            {
                await PageService.LoadDataflowEvents();
                UpdateGroups();
                StateHasChanged();
            });
        }
        catch (Exception) when (_tokenSource.IsCancellationRequested)
        {
            // The page was closed while reloading.
        }
        catch (Exception e)
        {
            await DispatchExceptionAsync(e);
        }
    }

    // A new filter opens every group and branch it found; they can then be closed again.
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
        _expandByDefault = OpensExpanded(grouping);
        _toggledGroups.Clear();
        UpdateGroups();
    }

    private void SetDirection(DataflowEventDirection direction)
    {
        _direction = direction;
        UpdateGroups();
    }

    private bool OpensExpanded(DataflowEventGrouping grouping)
        => grouping != DataflowEventGrouping.Container && PageService.Events.Count <= ExpandedEventLimit;

    // Every grouping but the container tree is one level of groups, each heading the events it holds.
    private void UpdateGroups()
    {
        _nodes = _grouping == DataflowEventGrouping.Container
            ? DataflowEventTree.Build(PageService.Events, _direction, _filter)
            : [.. DataflowEventGroups.Build(PageService.Events, _grouping, _direction, _filter).Select(g => new DataflowEventNode(g, [GroupLabel(g)], g.Events, []))];
        UpdateRows();
    }

    // The table renders only the rows in view, so it lists the visible headings and events of the tree as one list.
    private void UpdateRows()
    {
        List<TableRow> rows = [];
        if (_grouping == DataflowEventGrouping.None)
            rows.AddRange(_nodes.SelectMany(node => node.Events.Select(dataflowEvent => new TableRow(node, dataflowEvent, null, null, null))));
        else
            AddRows(rows, _nodes, level: 1, firstPosition: 1);

        // The heading row of the table is row 1.
        _rows = [.. rows.Select((row, i) => row with { RowIndex = i + 2 })];
        _ = _virtualize?.RefreshDataAsync();
    }

    // The rows come from memory at once; the ones the table asks for are the ones it renders, so their outputs are the
    // ones to subscribe.
    private ValueTask<ItemsProviderResult<TableRow>> ProvideRows(ItemsProviderRequest request)
    {
        // A request made before the rows shrank may start behind their end.
        var start = Math.Min(request.StartIndex, _rows.Count);
        var rows = _rows.GetRange(start, Math.Min(request.Count, _rows.Count - start));
        PageService.ShowEvents(rows.Where(row => row.Event is not null).Select(row => row.Event!));
        return ValueTask.FromResult(new ItemsProviderResult<TableRow>(rows, _rows.Count));
    }

    // Below an expanded node come its own events, then its containers, all counted as one set of siblings.
    private void AddRows(List<TableRow> rows, IReadOnlyList<DataflowEventNode> nodes, int level, int firstPosition)
    {
        var setSize = firstPosition - 1 + nodes.Count;
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            rows.Add(new(node, null, level, setSize, firstPosition + i));
            if (!IsExpanded(node))
                continue;

            var childCount = node.Events.Count + node.Children.Count;
            rows.AddRange(node.Events.Select((dataflowEvent, j) => new TableRow(node, dataflowEvent, level + 1, childCount, j + 1)));
            AddRows(rows, node.Children, level + 1, node.Events.Count + 1);
        }
    }

    // Without grouping there is nothing to collapse.
    private bool IsExpanded(DataflowEventNode node)
        => _grouping == DataflowEventGrouping.None || ExpandsByDefault != _toggledGroups.Contains(node.Group.Key);

    private bool ExpandsByDefault => IsFiltered ? _expandFilteredByDefault : _expandByDefault;

    private bool IsFiltered => !string.IsNullOrWhiteSpace(_filter);

    private void ToggleGroup(DataflowEventNode node)
    {
        if (!_toggledGroups.Add(node.Group.Key))
            _toggledGroups.Remove(node.Group.Key);

        UpdateRows();
    }

    private void SetExpansion(bool expand)
    {
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
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", _tokenSource.Token, dataflowEvent.ReadableValue);
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

    private static string? IndentStyle(int? level)
        => level is { } value ? string.Create(CultureInfo.InvariantCulture, $"--level: {value}") : null;

    // Containers lead, so function blocks of the same name in different containers stay apart at a glance.
    private string GroupLabel(DataflowEventGroup group)
    {
        var first = group.Events[0];
        return _grouping switch
        {
            DataflowEventGrouping.Dataflow => first.DataflowName,
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

    private void OnExpertViewChanged()
        => _ = InvokeAsync(StateHasChanged);

    // A value without a timestamp arrives with DateTime.MinValue; it shows no time instead of midnight.
    // The date shows only for a value from an earlier day, so today's values stay short; the Expert view adds the
    // milliseconds.
    private string TimeText(DateTime? timestamp)
    {
        if (timestamp is not { } value || value == default)
            return string.Empty;

        var zone = TimeProvider.LocalTimeZone;
        var received = AsUtc(value);
        var isToday = TimeZoneInfo.ConvertTime(received, zone).Date == TimeZoneInfo.ConvertTime(TimeProvider.GetUtcNow(), zone).Date;
        var time = isToday ? received.To24HourTime(zone) : received.ToShortDateAnd24HourTime(zone);
        return Expert ? $"{time}.{received.Millisecond:000}" : time;
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
    /// One row of the table: the heading of <see cref="Node"/> without an <see cref="Event"/>, or one of its events.
    /// <see cref="Level"/>, <see cref="SetSize"/> and <see cref="Position"/> place the row among its siblings in the tree;
    /// without grouping they are <c>null</c>.
    /// </summary>
    private readonly record struct TableRow(DataflowEventNode Node, DataflowEventModel? Event, int? Level, int? SetSize, int? Position)
    {
        public int RowIndex { get; init; }
    }

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
            PageService.EventsChanged -= OnEventsChanged;
            await PageService.DisposeAsync();
        }

        ExpertView?.Changed -= OnExpertViewChanged;

        await DisposeTreeKeys();
        await base.DisposeInternal();
    }

    // Leaving the page removes the table, and its key handling with it, before the page is disposed.
    private async Task DisposeTreeKeys()
    {
        if (_treeKeys is null)
            return;

        try
        {
            await _treeKeys.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone, and the page with it.
        }
    }
}
