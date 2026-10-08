using System.Globalization;
using Bunit;
using ClusterManagement.Client.Management.Components;
using ClusterManagement.Client.Management.Models;

namespace ClusterManagement.Client.Tests.Management;

public sealed class ClusterSummaryTilesTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }

    [Fact]
    public async Task Big_counts_are_shortened_and_exact_on_hover()
    {
        // Arrange
        await using var ctx = new BunitContext();
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        var summary = new ClusterTopologySummary(128, 1_024, 4_096, 16_384, 2_048, 123_456, 8_192, 9_876_543, 1_234_567);

        // Act
        var sut = ctx.Render<ClusterSummaryTiles>(p => p.Add(c => c.Summary, summary));

        // Assert
        var datapoints = sut.FindAll(".tile")[^1];
        datapoints.QuerySelector(".value")!.TextContent.Should().MatchRegex(@"^\s*1\.2 M\s*/ 9\.8 M\s*$");
        datapoints.GetAttribute("title").Should().StartWith("1,234,567 / 9,876,543");
        sut.FindAll(".tile")[5].QuerySelector(".value")!.TextContent.Trim().Should().Be("123.4 K");
        sut.FindAll(".tile")[3].QuerySelector(".value")!.TextContent.Trim().Should().Be("16,384");
        sut.FindAll(".tile")[3].GetAttribute("title").Should().BeNull();
    }
}
