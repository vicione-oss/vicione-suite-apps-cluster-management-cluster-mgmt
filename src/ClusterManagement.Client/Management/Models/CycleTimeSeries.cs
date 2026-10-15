namespace ClusterManagement.Client.Management.Models;

/// <summary>
/// Keeps the slowest cycle duration of every second per source, for the last <see cref="Seconds"/> seconds, and the
/// fastest and slowest cycle since the source began to report. A source without a report in that time is dropped, such
/// as the deployment id of a replaced version, so the series holds only what currently reports.
/// </summary>
/// <remarks>Written from the MQTT receive loop and read while rendering, so every member locks.</remarks>
public sealed class CycleTimeSeries(int seconds = 60)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, List<(long Second, double Milliseconds)>> _buckets = [];
    private readonly Dictionary<Guid, double> _latest = [];
    private readonly Dictionary<Guid, CycleRange> _ranges = [];
    private long _lastSweep;

    public int Seconds { get; } = seconds;

    /// <summary>
    /// Adds the chain of <paramref name="engineHostId"/> and every engine of the report under its deployment id.
    /// </summary>
    public void Add(Guid engineHostId, EngineCycleReport report, DateTimeOffset timestamp)
    {
        var second = timestamp.ToUnixTimeSeconds();

        lock (_lock)
        {
            AddLocked(engineHostId, report.CycleDuration, second);

            // A foreach over the interface would allocate an enumerator for every report.
            for (var i = 0; i < report.Deployments.Count; i++)
                AddLocked(report.Deployments[i].Deployment, report.Deployments[i].Duration, second);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _buckets.Clear();
            _latest.Clear();
            _ranges.Clear();
        }
    }

    /// <summary>
    /// The duration <paramref name="source"/> reported last; <c>null</c> once it has not reported for
    /// <see cref="Seconds"/> seconds up to <paramref name="now"/>, also when no other source reports.
    /// </summary>
    public double? Latest(Guid source, DateTimeOffset now)
    {
        lock (_lock)
        {
            DropSilentSourcesLocked(now.ToUnixTimeSeconds());
            return _latest.TryGetValue(source, out var value) ? value : null;
        }
    }

    /// <summary>
    /// The fastest and slowest cycle <paramref name="source"/> reported since it began to report; <c>null</c> before
    /// its first report, and once a later report or read of another source finds it silent for <see cref="Seconds"/>
    /// seconds.
    /// </summary>
    public CycleRange? Range(Guid source)
    {
        lock (_lock)
            return _ranges.TryGetValue(source, out var range) ? range : null;
    }

    /// <summary>
    /// The second <paramref name="source"/> reported last; <c>null</c> before its first report, and once a later report or
    /// read of another source finds it silent for <see cref="Seconds"/> seconds.
    /// </summary>
    public DateTimeOffset? LastReport(Guid source)
    {
        lock (_lock)
            return _buckets.TryGetValue(source, out var buckets) ? DateTimeOffset.FromUnixTimeSeconds(buckets[^1].Second) : null;
    }

    /// <summary>
    /// One entry per second up to <paramref name="now"/>, oldest first; <c>null</c> for seconds without a report.
    /// </summary>
    public IReadOnlyList<double?> Window(Guid source, DateTimeOffset now)
    {
        var last = now.ToUnixTimeSeconds();
        var first = last - Seconds + 1;
        var window = new double?[Seconds];

        lock (_lock)
        {
            if (!_buckets.TryGetValue(source, out var buckets))
                return window;

            foreach (var (second, milliseconds) in buckets)
            {
                if (second >= first && second <= last)
                    window[second - first] = milliseconds;
            }
        }

        return window;
    }

    private void AddLocked(Guid source, double milliseconds, long second)
    {
        DropSilentSourcesLocked(second);
        _latest[source] = milliseconds;
        _ranges[source] = _ranges.TryGetValue(source, out var range)
            ? range.With(milliseconds)
            : new CycleRange(milliseconds, milliseconds, DateTimeOffset.FromUnixTimeSeconds(second));

        if (!_buckets.TryGetValue(source, out var buckets))
            _buckets[source] = buckets = [];

        if (buckets.Count > 0 && buckets[^1].Second == second)
        {
            buckets[^1] = (second, Math.Max(buckets[^1].Milliseconds, milliseconds));
            return;
        }

        buckets.Add((second, milliseconds));
        if (buckets.Count > Seconds)
            buckets.RemoveAt(0);
    }

    // Runs at most once per second, so its cost does not grow with the report rate.
    private void DropSilentSourcesLocked(long second)
    {
        if (second <= _lastSweep)
            return;

        _lastSweep = second;
        var oldest = second - Seconds + 1;
        foreach (var (source, buckets) in _buckets)
        {
            if (buckets[^1].Second < oldest)
            {
                _buckets.Remove(source);
                _latest.Remove(source);
                _ranges.Remove(source);
            }
        }
    }
}

/// <summary>
/// The fastest and slowest cycle of a source in milliseconds, recorded since <paramref name="Since"/>.
/// </summary>
public readonly record struct CycleRange(double Fastest, double Slowest, DateTimeOffset Since)
{
    public CycleRange With(double milliseconds)
        => this with { Fastest = Math.Min(Fastest, milliseconds), Slowest = Math.Max(Slowest, milliseconds) };
}
