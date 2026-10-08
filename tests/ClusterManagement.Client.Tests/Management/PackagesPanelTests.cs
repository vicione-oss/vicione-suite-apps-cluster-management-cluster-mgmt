using System.Globalization;
using Bunit;
using ClusterManagement.Client.Management.Components;
using ClusterManagement.Client.Management.Localization;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Tests.Management;

public class PackagesPanelTests
{
    [Fact]
    public async Task Long_lists_show_the_first_eight_packages_by_name()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var dependencies = Dependencies(12);

        // Act
        var sut = Render(ctx, dependencies);

        // Assert
        sut.FindAll(".row .name").Select(n => n.TextContent).Should().Equal(dependencies.Select(d => d.Name).Order(StringComparer.OrdinalIgnoreCase).Take(8));
        sut.Find(".list-footer button").TextContent.Trim().Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.ShowAllPackages, 12));
    }

    [Fact]
    public async Task Show_all_lists_every_package()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = Render(ctx, Dependencies(12));

        // Act
        await sut.Find(".list-footer button").ClickAsync(new());

        // Assert
        sut.FindAll(".row").Should().HaveCount(12);
        sut.Find(".list-footer button").TextContent.Trim().Should().Be(ClusterOverview.ShowFewer);
    }

    [Fact]
    public async Task Short_lists_have_no_show_all()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = Render(ctx, Dependencies(8));

        // Assert
        sut.FindAll(".row").Should().HaveCount(8);
        sut.FindAll(".list-footer").Should().BeEmpty();
    }

    private static IRenderedComponent<PackagesPanel> Render(BunitContext ctx, List<ClusterDependency> dependencies)
        => ctx.Render<PackagesPanel>(p => p
            .Add(c => c.Version, new Version(0, 1, 1))
            .Add(c => c.Dependencies, dependencies));

    private static List<ClusterDependency> Dependencies(int count)
        => [.. Enumerable.Range(0, count).Reverse().Select(i => new ClusterDependency { Name = $"Package.{i:D2}", Version = "1.0.0" })];
}
