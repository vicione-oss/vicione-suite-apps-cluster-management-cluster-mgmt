using System.Globalization;
using ClusterManagement.Client.Management.Extensions;

namespace ClusterManagement.Client.Tests.Management;

public class TimeSpanExtensionsTests
{
    [Theory]
    [InlineData(12, "< 0.1 s")]
    [InlineData(227, "0.2 s")]
    [InlineData(2_455, "2.5 s")]
    [InlineData(192_000, "3 min 12 s")]
    [InlineData(7_500_000, "2 h 5 min")]
    public void Display_text_uses_the_largest_fitting_units(int milliseconds, string expected)
        => TimeSpan.FromMilliseconds(milliseconds).ToDisplayText(CultureInfo.InvariantCulture).Should().Be(expected);

    [Fact]
    public void Display_text_uses_the_decimal_separator_of_the_culture()
        => TimeSpan.FromMilliseconds(2_455).ToDisplayText(CultureInfo.GetCultureInfo("de-DE")).Should().Be("2,5 s");
}
