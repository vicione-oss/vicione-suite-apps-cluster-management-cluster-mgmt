using System.Globalization;
using ClusterManagement.Client.Management.Localization;

namespace ClusterManagement.Client.Management.Extensions;

internal static class CountFormatter
{
    /// <summary>
    /// Counts from this value on are shortened, so a tile keeps one line.
    /// </summary>
    public const int CompactFrom = 100_000;

    public static bool IsCompact(int count)
        => Math.Abs(count) >= CompactFrom;

    /// <summary>
    /// The exact count with group separators, or a short form such as <c>1.2 M</c> from <see cref="CompactFrom"/> on.
    /// </summary>
    public static string Format(int count, CultureInfo culture)
    {
        if (!IsCompact(count))
            return count.ToString("N0", culture);

        var (value, resource) = Math.Abs(count) switch
        {
            >= 1_000_000_000 => (count / 1_000_000_000d, nameof(ClusterOverview.CompactBillions)),
            >= 1_000_000 => (count / 1_000_000d, nameof(ClusterOverview.CompactMillions)),
            _ => (count / 1_000d, nameof(ClusterOverview.CompactThousands)),
        };

        // The suffix comes from the same culture as the number, so "1,2 Mio." never turns into "1,2 M" or "1.2 Mio.".
        var format = ClusterOverview.ResourceManager.GetString(resource, culture) ?? "{0}";
        return string.Format(culture, format, Math.Floor(value * 10) / 10);
    }
}
