using System.Globalization;
using System.Text;
using ClusterManagement.Client.Management.Localization;
using Microsoft.AspNetCore.Components;

namespace ClusterManagement.Client.Management.Components;

/// <summary>
/// The slowest cycle per second as a line; the part above <see cref="Limit"/> is highlighted.
/// </summary>
/// <remarks>
/// A line keeps the render diff to one attribute per segment, which matters because the chart moves every second
/// and every change travels over the circuit.
/// </remarks>
public sealed partial class CycleTimeChart
{
    private const int Width = 120;
    private const int Height = 28;

    private readonly string _overLimitClipId = $"cycle-over-{Guid.NewGuid():N}";

    private IReadOnlyList<string> _segments = [];
    private double? _limitY;
    private bool _hasOverLimit;
    private string _summary = string.Empty;
    private string? _renderedSignature;
    private bool _hasChanges = true;

    [Parameter, EditorRequired] public IReadOnlyList<double?> Samples { get; set; } = [];

    /// <summary>
    /// Configured cycle time in milliseconds; <c>null</c> or zero draws no limit line.
    /// </summary>
    [Parameter] public double? Limit { get; set; }

    protected override void OnParametersSet()
    {
        var max = Max(Samples);
        var scale = max is > 0 ? (Height - 2) / max.Value : 0;

        _segments = Segments(Samples, scale);
        _limitY = Limit is > 0 && Limit.Value * scale <= Height ? Height - (Limit.Value * scale) : null;
        _hasOverLimit = Limit is > 0 && max > Limit;
        _summary = max is { } slowest
            ? string.Format(CultureInfo.CurrentCulture, ClusterOverview.CycleChartSummary, slowest, Samples.Count, Limit ?? 0)
            : ClusterOverview.CycleChartNoData;

        var signature = $"{string.Join('|', _segments)};{_limitY};{_summary}";
        _hasChanges = signature != _renderedSignature;
        _renderedSignature = signature;
    }

    // Engines that did not change, such as stopped ones, are not rendered again every second.
    protected override bool ShouldRender()
        => _hasChanges;

    private static double? Max(IReadOnlyList<double?> samples)
    {
        double? max = null;
        foreach (var sample in samples)
        {
            if (sample > max || (max is null && sample is not null))
                max = sample;
        }

        return max;
    }

    /// <summary>
    /// One polyline per run of seconds with a cycle; a second without a report breaks the line.
    /// </summary>
    private static List<string> Segments(IReadOnlyList<double?> samples, double scale)
    {
        var segments = new List<string>();
        var step = samples.Count == 0 ? 0 : (double)Width / samples.Count;
        var points = new StringBuilder();
        var pointCount = 0;

        for (var i = 0; i <= samples.Count; i++)
        {
            if (i < samples.Count && samples[i] is { } sample)
            {
                var x = (i * step) + (step / 2);
                var y = Height - 1 - (sample * scale);
                points.Append(points.Length == 0 ? string.Empty : " ").Append(Svg(x)).Append(',').Append(Svg(y));
                pointCount++;
                continue;
            }

            // A single point needs a second one to be drawn.
            if (pointCount == 1)
            {
                var point = points.ToString();
                points.Append(' ').Append(point);
            }

            if (points.Length > 0)
                segments.Add(points.ToString());

            points.Clear();
            pointCount = 0;
        }

        return segments;
    }

    /// <summary>
    /// SVG coordinates need a dot as decimal separator whatever the UI culture.
    /// </summary>
    private static string Svg(double value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);
}
