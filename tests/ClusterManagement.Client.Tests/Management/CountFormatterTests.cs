using System.Globalization;
using ClusterManagement.Client.Management.Extensions;

namespace ClusterManagement.Client.Tests.Management;

public class CountFormatterTests
{
    private static readonly CultureInfo s_english = CultureInfo.GetCultureInfo("en-US");

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1_024, "1,024")]
    [InlineData(99_999, "99,999")]
    [InlineData(100_000, "100 K")]
    [InlineData(123_456, "123.4 K")]
    [InlineData(1_234_567, "1.2 M")]
    [InlineData(9_999_999, "9.9 M")]
    [InlineData(2_147_483_647, "2.1 B")]
    public void Shortens_counts_from_one_hundred_thousand_on(int count, string expected)
        => CountFormatter.Format(count, s_english).Should().Be(expected);

    [Fact]
    public void Short_form_is_rounded_down_so_it_never_overstates_the_count()
        => CountFormatter.Format(1_999_999, s_english).Should().Be("1.9 M");

    [Fact]
    public void Short_form_follows_the_culture_in_number_and_suffix()
        => CountFormatter.Format(1_234_567, CultureInfo.GetCultureInfo("de-DE")).Should().Be("1,2 Mio.");
}
