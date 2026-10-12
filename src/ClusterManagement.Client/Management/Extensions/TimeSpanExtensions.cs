using System.Globalization;

namespace ClusterManagement.Client.Management.Extensions;

internal static class TimeSpanExtensions
{
    public static string ToDisplayText(this TimeSpan duration, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;

        if (duration.TotalSeconds < 0.1)
            return string.Format(provider, "< {0:0.0} s", 0.1);

        if (duration.TotalMinutes < 1)
            return string.Format(provider, "{0:0.0} s", duration.TotalSeconds);

        if (duration.TotalHours < 1)
            return string.Format(provider, "{0} min {1} s", (int)duration.TotalMinutes, duration.Seconds);

        return string.Format(provider, "{0} h {1} min", (int)duration.TotalHours, duration.Minutes);
    }

    /// <summary>
    /// A running time like a stopwatch shows it: <c>0:37</c>, <c>12:05</c> or <c>1:02:09</c>.
    /// </summary>
    public static string ToStopwatchText(this TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        return elapsed.TotalHours < 1
            ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", (int)elapsed.TotalMinutes, elapsed.Seconds)
            : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", (int)elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds);
    }
}
