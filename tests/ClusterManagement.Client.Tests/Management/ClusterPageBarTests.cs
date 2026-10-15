using System.Globalization;
using Bunit;
using ClusterManagement.Client.Management.Components;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Public;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ViciOne.Ui.Blazor.Components.Breadcrumb.Components;
using ViciOne.Ui.Blazor.Components.Toolbar.Components;

namespace ClusterManagement.Client.Tests.Management;

public class ClusterPageBarTests
{
    private readonly IClusterManagementService _managementService = Substitute.For<IClusterManagementService>();
    private readonly ExpertViewState _expertView = new();

    [Fact]
    public async Task Starts_the_trail_at_the_cluster()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = Render(ctx, ClusterPage.Overview);

        // Assert
        sut.FindAll(".breadcrumb-item").Select(i => i.TextContent.Trim()).Should().Equal("Cluster");
        sut.Find("h1").TextContent.Should().Be("Cluster");
    }

    [Fact]
    public async Task Starts_the_trail_at_the_Cluster_Manager_until_the_cluster_is_known()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = Render(ctx, ClusterPage.EventViewer, clusterName: null);

        // Assert
        sut.FindAll(".breadcrumb-item").Select(i => i.TextContent.Trim()).Should().Equal(Common.ModuleTitle, ClusterOverview.EventViewer);
    }

    [Fact]
    public async Task Names_the_page_after_the_cluster()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = Render(ctx, ClusterPage.EventViewer);

        // Assert
        sut.FindAll(".breadcrumb-item").Select(i => i.TextContent.Trim()).Should().Equal("Cluster", ClusterOverview.EventViewer);
        sut.Find(".breadcrumb-item.disabled").TextContent.Trim().Should().Be(ClusterOverview.EventViewer);
        sut.Find("h1").TextContent.Should().Be(ClusterOverview.EventViewer);
    }

    [Fact]
    public async Task Shows_no_separator_on_the_overview()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = Render(ctx, ClusterPage.Overview);

        // Assert
        sut.FindAll(".breadcrumb-item-separator").Should().BeEmpty();
    }

    [Fact]
    public async Task Keeps_the_engines_in_the_trail_while_the_user_is_on_them()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = Render(ctx, ClusterPage.Engines);

        // Assert
        sut.FindAll(".breadcrumb-item").Select(i => i.TextContent.Trim()).Should().Equal("Cluster", ClusterOverview.Engines);
    }

    [Fact]
    public async Task Keeps_the_trail_while_the_page_renders_again()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = Render(ctx, ClusterPage.EventViewer);
        var trail = sut.FindComponent<Breadcrumb>().Instance.CurrentItem;

        // Act
        sut.Render(p => p.Add(c => c.RunningVersion, new Version(0, 1, 1)));

        // Assert
        sut.FindComponent<Breadcrumb>().Instance.CurrentItem.Should().BeSameAs(trail);
    }

    [Fact]
    public async Task Goes_back_to_the_cluster_from_the_trail()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = Render(ctx, ClusterPage.EventViewer);

        // Act
        await sut.Find(".breadcrumb-item:not(.disabled)").ClickAsync(new());

        // Assert
        ctx.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith(Constants.ManagementModuleRoute);
    }

    [Fact]
    public async Task Switches_the_expert_view_on_for_the_session()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = Render(ctx, ClusterPage.Overview);
        var toggle = sut.FindComponents<ToolbarButton>().Single(b => b.Instance.Text == ClusterOverview.ExpertView);

        // Act
        await sut.InvokeAsync(toggle.Instance.OnClick.InvokeAsync);

        // Assert
        _expertView.IsExpert.Should().BeTrue();
        sut.WaitForAssertion(() => toggle.Instance.Active.Should().BeTrue());
    }

    [Fact]
    public async Task Shows_a_draft_that_is_ready_to_deploy()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = Render(ctx, ClusterPage.Overview, draft: new Version(0, 1, 2));

        // Assert
        sut.Find(".chip.status-draft").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.DraftReady, "0.1.2"));
    }

    [Fact]
    public async Task Shows_no_draft_without_one()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = Render(ctx, ClusterPage.Overview);

        // Assert
        sut.FindAll(".chip.status-draft").Should().BeEmpty();
    }

    private IRenderedComponent<ClusterPageBar> Render(BunitContext ctx, ClusterPage page, string? clusterName = "Cluster", Version? draft = null)
    {
        ctx.AddClusterPageBar(_expertView);
        ctx.Services.AddClientTimeProvider(TimeZoneInfo.Utc);
        ctx.Services.AddSingleton(_managementService);
        ctx.Services.AddSingleton(Substitute.For<ILogger<ClusterStatus>>());

        return ctx.Render<ClusterPageBar>(p => p
            .Add(c => c.Page, page)
            .Add(c => c.ClusterName, clusterName)
            .Add(c => c.ClusterId, Guid.NewGuid())
            .Add(c => c.RunningVersion, new Version(0, 1, 1))
            .Add(c => c.DraftVersion, draft));
    }
}
