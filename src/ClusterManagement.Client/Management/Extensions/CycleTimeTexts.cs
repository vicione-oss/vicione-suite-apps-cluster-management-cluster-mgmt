using System.Globalization;
using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;

namespace ClusterManagement.Client.Management.Extensions;

/// <summary>
/// How cycle times read on the Cluster Manager pages.
/// </summary>
internal static class CycleTimeTexts
{
    public static string Milliseconds(double? milliseconds)
        => milliseconds is { } value ? $"{Number(value)} ms" : "–";

    // A cycle below one millisecond keeps two decimals, so it does not read as 0.0.
    public static string Number(double milliseconds)
        => milliseconds.ToString(milliseconds < 1 ? "0.00" : "0.0", CultureInfo.CurrentCulture);

    /// <summary>
    /// The latest cycle of the chain of <paramref name="host"/> and how much of its cycle time it used.
    /// </summary>
    public static string Usage(EngineHostRuntimeModel host, double latestChainCycle)
        => string.Format(CultureInfo.CurrentCulture, ClusterOverview.HostCycleUsage, Milliseconds(latestChainCycle), host.CycleTime,
            latestChainCycle / host.CycleTime * 100);

    /// <summary>
    /// The fastest and the slowest cycle of <paramref name="range"/>.
    /// </summary>
    public static string Range(CycleRange range)
        => $"{Number(range.Fastest)} / {Milliseconds(range.Slowest)}";

    /// <summary>
    /// Since when <paramref name="range"/> was recorded, in <paramref name="timeZone"/>: from the first report the module
    /// received from its source after the source was last silent.
    /// </summary>
    public static string RangeHint(CycleRange range, TimeZoneInfo timeZone)
        => string.Format(CultureInfo.CurrentCulture, ClusterOverview.CycleRangeHint, range.Since.To24HourTime(timeZone));

    /// <summary>
    /// Whether the chain of <paramref name="host"/> took longer than its cycle time in <paramref name="chainWindow"/>.
    /// </summary>
    public static bool IsOverCycleTime(this EngineHostRuntimeModel host, IReadOnlyList<double?> chainWindow)
        => host.CycleTime > 0 && chainWindow.Any(sample => sample > host.CycleTime);
}
