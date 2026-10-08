using System.Globalization;
using Bunit;
using ClusterManagement.Client.Management.Components;

namespace ClusterManagement.Client.Tests.Management;

public sealed class CycleTimeChartTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public void Dispose()
        => CultureInfo.CurrentCulture = _culture;

    [Fact]
    public async Task Draws_one_line_per_run_of_seconds_with_a_cycle()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = Render(ctx, [2, 6, null, 3, 4], limit: null);

        // Assert
        sut.FindAll("polyline.line").Select(p => p.GetAttribute("points")!.Split(' ').Length).Should().Equal(2, 2);
    }

    [Fact]
    public async Task Highlights_the_line_above_the_limit()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = Render(ctx, [2, 6, 3], limit: 5);

        // Assert
        sut.FindAll("line.limit").Should().ContainSingle();
        var over = sut.FindAll("polyline.over").Should().ContainSingle().Which;
        over.GetAttribute("clip-path").Should().Be($"url(#{sut.Find("clipPath").Id})");
    }

    [Fact]
    public async Task Draws_no_limit_line_above_the_measured_cycles()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = Render(ctx, [0.2, 0.3], limit: 500);

        // Assert
        sut.FindAll("line.limit").Should().BeEmpty();
        sut.FindAll("polyline.over").Should().BeEmpty();
    }

    [Fact]
    public async Task Draws_a_single_cycle_as_a_point()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = Render(ctx, [null, 3, null], limit: null);

        // Assert
        var points = sut.Find("polyline.line").GetAttribute("points")!.Split(' ');
        points.Should().HaveCount(2);
        points[1].Should().Be(points[0]);
    }

    [Fact]
    public async Task Writes_coordinates_with_a_dot_in_every_culture()
    {
        // Arrange
        await using var ctx = new BunitContext();
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        // Act
        var sut = Render(ctx, [1, 3, 2, 4, 1, 2, 3], limit: null);

        // Assert
        var points = sut.Find("polyline.line").GetAttribute("points")!.Split(' ');
        points.Should().HaveCount(7).And.AllSatisfy(point => point.Split(',').Should().HaveCount(2));
        points.Should().Contain(point => point.Contains('.'));
    }

    private static IRenderedComponent<CycleTimeChart> Render(BunitContext ctx, double?[] samples, double? limit)
        => ctx.Render<CycleTimeChart>(p => p
            .Add(c => c.Samples, samples)
            .Add(c => c.Limit, limit));
}
