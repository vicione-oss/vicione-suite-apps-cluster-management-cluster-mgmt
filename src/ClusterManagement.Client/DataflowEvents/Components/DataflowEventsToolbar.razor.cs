using System.Globalization;
using ClusterManagement.Client.DataflowEvents.Models;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.ColorableIcons;
using ViciOne.Ui.Localization.Resources;

namespace ClusterManagement.Client.DataflowEvents.Components;

/// <summary>
/// Heads the dataflow events table: how its events are grouped and filtered, and how its groups are opened.
/// </summary>
/// <remarks>
/// The table renders whenever values arrive; the toolbar renders only when what it shows changes, so the values do not
/// render its buttons again and again.
/// </remarks>
public sealed partial class DataflowEventsToolbar
{
    private const int LegendIconSize = 24;
    private const string LegendColor = "rgb(140, 140, 140)";

    private static readonly MarkupString s_legendInputIcon = new(ColoredIconFactory.GetConnectorIcon(LegendColor, true, LegendIconSize));
    private static readonly MarkupString s_legendOutputIcon = new(ColoredIconFactory.GetConnectorIcon(LegendColor, false, LegendIconSize));

    private readonly string _expandAllGroupsText = string.Format(CultureInfo.CurrentCulture, UserActions.ExpandSomething, $"{CommonVocabulary.All} {CommonVocabulary.GroupPlural}");
    private readonly string _collapseAllGroupsText = string.Format(CultureInfo.CurrentCulture, UserActions.CollapseSomething, $"{CommonVocabulary.All} {CommonVocabulary.GroupPlural}");

    private ShownState? _rendered;

    [Parameter] public DataflowEventGrouping Grouping { get; set; }
    [Parameter] public DataflowEventDirection Direction { get; set; }
    [Parameter] public string? Filter { get; set; }
    [Parameter] public int ShownCount { get; set; }
    [Parameter] public int TotalCount { get; set; }

    /// <summary>
    /// Whether a group is open; the one expansion button then closes every group, otherwise it opens them.
    /// </summary>
    [Parameter] public bool AnyGroupExpanded { get; set; }

    [Parameter] public EventCallback<DataflowEventGrouping> GroupingChanged { get; set; }
    [Parameter] public EventCallback<DataflowEventDirection> DirectionChanged { get; set; }
    [Parameter] public EventCallback<string?> FilterChanging { get; set; }

    /// <summary>
    /// Opens (<c>true</c>) or closes (<c>false</c>) every group.
    /// </summary>
    [Parameter] public EventCallback<bool> ExpansionRequested { get; set; }

    private bool IsGrouped => Grouping != DataflowEventGrouping.None;

    private string ExpansionText => AnyGroupExpanded ? _collapseAllGroupsText : _expandAllGroupsText;

    private string ShownText => string.Format(CultureInfo.CurrentCulture, Localization.DataflowEventsPage.EventsShown, ShownCount, TotalCount);

    // Every parameter the markup reads, so a change of any of them renders the toolbar.
    private ShownState Shown => new(Grouping, Direction, Filter, ShownCount, TotalCount, AnyGroupExpanded);

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

    protected override void OnInitialized() => _rendered = Shown;

    // Noted when the render is decided, not after it reached the browser: a change in between renders again.
    protected override bool ShouldRender()
    {
        if (_rendered == Shown)
            return false;

        _rendered = Shown;
        return true;
    }

    private readonly record struct ShownState(DataflowEventGrouping Grouping, DataflowEventDirection Direction, string? Filter,
        int ShownCount, int TotalCount, bool AnyGroupExpanded);
}
