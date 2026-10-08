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

    private Tile[] Tiles => [
        new(Summary.Nodes, null, ClusterOverview.Nodes, null),
        new(Summary.Instances, null, TechnicalTerms.InstancePlural, null),
        new(Summary.EngineHosts, null, ClusterOverview.EngineHosts, null),
        new(Summary.Engines, null, ClusterOverview.Engines, null),
        new(Summary.Dataflows, null, ClusterOverview.Dataflows, null),
        new(Summary.FunctionBlocks, null, ClusterOverview.FunctionBlocks, null),
        new(Summary.DataPorts, null, ClusterOverview.DataPorts, null),
        new(Summary.LinkedDatapoints, Summary.Datapoints, ClusterOverview.Datapoints, ClusterOverview.LinkedDatapointsHint),
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
    private sealed record Tile(int Value, int? Of, string Label, string? Hint);
}
