using Bunit;
using ClusterManagement.Client.DataflowEvents.Components;
using ClusterManagement.Client.DataflowEvents.Models;
using Sdk.Client.Modules.Localization.Extensions;
using Sdk.Testing.Client;
using ViciOne.Ui.Blazor.Components.TestingHelpers.TextBox.Extensions;

namespace ClusterManagement.Client.Tests.DataflowEvents.Components;

public class DataflowEventsToolbarTests
{
    [Fact]
    public async Task Does_not_render_again_while_what_it_shows_stays_the_same()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = Render(ctx);
        var renders = sut.RenderCount;

        // Act
        sut.Render(p => p.Add(c => c.ShownCount, 3));

        // Assert
        sut.RenderCount.Should().Be(renders);
    }

    [Theory]
    [InlineData("filter")]
    [InlineData("count")]
    [InlineData("expanded")]
    public async Task Renders_again_when_what_it_shows_changes(string change)
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = Render(ctx);
        var renders = sut.RenderCount;

        // Act
        sut.Render(p =>
        {
            if (change == "filter")
                p.Add(c => c.Filter, "Valve");
            else if (change == "count")
                p.Add(c => c.ShownCount, 2);
            else
                p.Add(c => c.AnyGroupExpanded, true);
        });

        // Assert
        sut.RenderCount.Should().BeGreaterThan(renders);
    }

    private static IRenderedComponent<DataflowEventsToolbar> Render(BunitContext ctx)
    {
        ctx.SetupSuiteServices(setup => setup.Services.AddLocalization<ClusterManagementClientModule, ClusterManagementClientLocalizer>());
        ctx.AddClusterPageBar();
        ctx.JSInterop.SetupForTextBox();

        return ctx.Render<DataflowEventsToolbar>(p => p
            .Add(c => c.Grouping, DataflowEventGrouping.Dataflow)
            .Add(c => c.Direction, DataflowEventDirection.All)
            .Add(c => c.ShownCount, 3)
            .Add(c => c.TotalCount, 5));
    }
}
