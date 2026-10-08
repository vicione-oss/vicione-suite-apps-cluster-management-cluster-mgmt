using System.Globalization;

namespace ClusterManagement.Client.Extensions;

internal static class DateTimeOffsetExtensions
{
    /// <summary>
    /// Converts the value to <paramref name="timeZone"/> and formats it as the current culture's short date followed by the 24-hour time,
    /// e.g. <c>30.09.2026 14:05:33</c>.
    /// </summary>
    public static string ToShortDateAnd24HourTime(this DateTimeOffset value, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTime(value, timeZone);
        return $"{local.ToString("d", CultureInfo.CurrentCulture)} {local.ToString("HH:mm:ss", CultureInfo.CurrentCulture)}";
    }

    /// <summary>
    /// Converts the value to <paramref name="timeZone"/> and formats it as the 24-hour time, e.g. <c>14:05:33</c>.
    /// </summary>
    public static string To24HourTime(this DateTimeOffset value, TimeZoneInfo timeZone)
        => TimeZoneInfo.ConvertTime(value, timeZone).ToString("HH:mm:ss", CultureInfo.CurrentCulture);
}
