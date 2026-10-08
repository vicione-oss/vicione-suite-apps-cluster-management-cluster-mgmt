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
}
