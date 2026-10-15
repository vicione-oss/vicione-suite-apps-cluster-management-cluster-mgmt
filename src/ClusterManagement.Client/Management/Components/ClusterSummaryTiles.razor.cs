using System.Globalization;
using ClusterManagement.Client.Management.Extensions;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Localization.Resources;

namespace ClusterManagement.Client.Management.Components;

public sealed partial class ClusterSummaryTiles
{
    [Parameter, EditorRequired] public ClusterTopologySummary Summary { get; set; } = default!;

    /// <summary>
    /// Adds the topology behind the engines: nodes, instances, engine hosts and function blocks.
    /// </summary>
    [Parameter] public bool Expert { get; set; }

    private Tile[] Tiles => Expert ? AllTiles : [.. AllTiles.Where(t => t.InStandardView)];

    private Tile[] AllTiles => [
        new(Summary.Nodes, null, ClusterOverview.Nodes, ClusterOverview.Node, null, InStandardView: false),
        new(Summary.Instances, null, TechnicalTerms.InstancePlural, ClusterOverview.Instance, null, InStandardView: false),
        new(Summary.EngineHosts, null, ClusterOverview.EngineHosts, ClusterOverview.EngineHost, null, InStandardView: false),
        new(Summary.Engines, null, ClusterOverview.Engines, ClusterOverview.Engine, null, InStandardView: true),
        new(Summary.Dataflows, null, ClusterOverview.Dataflows, ClusterOverview.Dataflow, null, InStandardView: true),
        new(Summary.FunctionBlocks, null, ClusterOverview.FunctionBlocks, ClusterOverview.FunctionBlock, null, InStandardView: false),
        new(Summary.DataPorts, null, ClusterOverview.DataPorts, ClusterOverview.DataPort, null, InStandardView: true),
        new(Summary.LinkedDatapoints, Summary.Datapoints, ClusterOverview.Datapoints, ClusterOverview.Datapoints, ClusterOverview.LinkedDatapointsHint, InStandardView: true),
    ];

    private static string Format(int count)
        => CountFormatter.Format(count, CultureInfo.CurrentCulture);

    // Long values such as "12.345 / 45.678" step the font down instead of being cut off.
    private static string? SizeClass(Tile tile)
    {
        var length = Format(tile.Value).Length + (tile.Of is { } of ? Format(of).Length + 3 : 0);
        return length switch
        {
            > 14 => "size-s",
            > 9 => "size-m",
            _ => null,
        };
    }

    // A shortened count shows its exact value on hover.
    private static string? Tooltip(Tile tile)
    {
        var compact = CountFormatter.IsCompact(tile.Value) || (tile.Of is { } of && CountFormatter.IsCompact(of));
        if (!compact)
            return tile.Hint;

        var exact = tile.Of is { } total
            ? $"{tile.Value.ToString("N0", CultureInfo.CurrentCulture)} / {total.ToString("N0", CultureInfo.CurrentCulture)}"
            : tile.Value.ToString("N0", CultureInfo.CurrentCulture);

        return tile.Hint is null ? exact : $"{exact}{Environment.NewLine}{tile.Hint}";
    }

    /// <summary>
    /// <c>Of</c> is the total the value is part of, shown as "value / of".
    /// </summary>
    private sealed record Tile(int Value, int? Of, string Label, string OneLabel, string? Hint, bool InStandardView)
    {
        // A count of one is named in the singular; a tile with a total, such as "1 / 3", keeps the plural.
        public string Text => Value == 1 && Of is null ? OneLabel : Label;
    }
}
