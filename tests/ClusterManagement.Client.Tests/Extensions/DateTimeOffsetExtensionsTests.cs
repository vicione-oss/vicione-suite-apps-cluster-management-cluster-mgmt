using System.Globalization;
using ClusterManagement.Client.Extensions;

namespace ClusterManagement.Client.Tests.Extensions;

public class DateTimeOffsetExtensionsTests
{
    [Theory]
    [InlineData("de-DE", "30.09.2026 14:05:33")]
    [InlineData("en-US", "9/30/2026 14:05:33")]
    public void Should_format_short_date_and_24_hour_time(string culture, string expected)
    {
        // Arrange
        var value = new DateTimeOffset(2026, 9, 30, 14, 5, 33, TimeSpan.Zero);
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);

        try
        {
            // Act
            var formatted = value.ToShortDateAnd24HourTime(TimeZoneInfo.Utc);

            // Assert
            formatted.Should().Be(expected);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void Should_convert_to_the_time_zone_before_formatting()
    {
        // Arrange
        var value = new DateTimeOffset(2026, 9, 30, 12, 5, 33, TimeSpan.Zero);
        var timeZone = TimeZoneInfo.CreateCustomTimeZone("UTC+02", TimeSpan.FromHours(2), "UTC+02", "UTC+02");
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        try
        {
            // Act
            var formatted = value.ToShortDateAnd24HourTime(timeZone);

            // Assert
            formatted.Should().Be("30.09.2026 14:05:33");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}
