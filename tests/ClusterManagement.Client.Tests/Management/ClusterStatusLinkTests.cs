using System.Globalization;
using Bunit;
using ClusterManagement.Client.Management.Components;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Public;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClusterManagement.Client.Tests.Management;

public class ClusterStatusLinkTests
{
    private static readonly Guid s_clusterId = Guid.NewGuid();
    private static readonly Version s_running = new(0, 1, 32);
    private static readonly Version s_target = new(0, 1, 33);

    private readonly IClusterManagementService _managementService = Substitute.For<IClusterManagementService>();

    [Fact]
    public async Task Links_back_to_the_Cluster_Manager_with_the_running_version()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = Render(ctx);

        // Assert
        var link = sut.Find("a.cluster-status-link");
        link.GetAttribute("href").Should().Be(Constants.ManagementModuleRoute);
        link.GetAttribute("title").Should().NotBeNullOrEmpty().And.Be(ClusterOverview.BackToClusterManager);
        sut.Find(".status").TextContent.Should().Be(Format(ClusterOverview.RunningVersionDeployed, s_running));
        sut.Find(".dot").ClassList.Should().Contain("status-running");
    }

    [Fact]
    public async Task Says_when_no_version_runs()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = Render(ctx, isRunning: false);

        // Assert
        sut.Find(".status").TextContent.Should().Be(ClusterOverview.NoVersionRunning);
    }

    [Fact]
    public async Task Shows_no_status_while_the_page_loads()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = Render(ctx, isRunning: false, isLoading: true);

        // Assert
        sut.Find(".status").TextContent.Should().Be("…");
    }

    [Fact]
    public async Task Shows_a_deployment_with_its_phase()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = Render(ctx);

        // Act
        Start(new ClusterUpdateStarted(Guid.NewGuid(), s_clusterId, s_target));
        _managementService.OnClusterUpdateChanged += Raise.Event<EventHandler<ClusterUpdateChanged>>(_managementService,
            new ClusterUpdateChanged(Guid.NewGuid(), s_clusterId, s_target, "UpdatingDeployments") { Change = ClusterUpdateChange.Transition });

        // Assert
        sut.WaitForAssertion(() =>
        {
            sut.Find(".status").TextContent.Should().Be($"{Format(ClusterOverview.DeployingVersion, s_target)} · {ClusterOverview.PhaseUpdatingDeployments}");
            sut.Find(".dot").ClassList.Should().Contain("status-deploying");
        });
    }

    [Fact]
    public async Task Shows_the_running_version_with_a_deployment_that_failed_and_rolled_back()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = Render(ctx);
        Start(new ClusterUpdateStarted(Guid.NewGuid(), s_clusterId, s_target));

        // Act
        _managementService.OnClusterUpdateFailed += Raise.Event<EventHandler<ClusterUpdateFailed>>(_managementService,
            new ClusterUpdateFailed(Guid.NewGuid(), s_clusterId, s_target) { State = "RollingBackDeployments" });

        // Assert
        sut.WaitForAssertion(() =>
        {
            sut.Find(".status").TextContent.Should().Be(
                $"{Format(ClusterOverview.RunningVersionDeployed, s_running)} · {Format(ClusterOverview.DeploymentFailed, s_target)}");
            sut.Find(".dot").ClassList.Should().Contain("status-running");
        });
    }

    [Fact]
    public async Task Shows_a_failed_deployment_when_no_version_runs()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = Render(ctx, isRunning: false);
        Start(new ClusterUpdateStarted(Guid.NewGuid(), s_clusterId, s_target));

        // Act
        _managementService.OnClusterUpdateFailed += Raise.Event<EventHandler<ClusterUpdateFailed>>(_managementService,
            new ClusterUpdateFailed(Guid.NewGuid(), s_clusterId, s_target) { State = "RollingBackDeployments" });

        // Assert
        sut.WaitForAssertion(() =>
        {
            sut.Find(".status").TextContent.Should().Be(Format(ClusterOverview.DeploymentFailed, s_target));
            sut.Find(".dot").ClassList.Should().Contain("status-failed");
        });
    }

    [Fact]
    public async Task Shows_the_running_version_again_once_the_deployment_completes()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = Render(ctx);
        Start(new ClusterUpdateStarted(Guid.NewGuid(), s_clusterId, s_target));
        sut.WaitForAssertion(() => sut.Find(".dot").ClassList.Should().Contain("status-deploying"));

        // Act
        _managementService.OnClusterUpdateCompleted += Raise.Event<EventHandler<ClusterUpdateCompleted>>(_managementService,
            new ClusterUpdateCompleted(Guid.NewGuid(), s_clusterId, s_target, TimeSpan.FromSeconds(3)));

        // Assert
        sut.WaitForAssertion(() => sut.Find(".dot").ClassList.Should().Contain("status-running"));
    }

    [Fact]
    public async Task Shows_a_deployment_that_already_runs_when_the_page_opens()
    {
        // Arrange
        await using var ctx = new BunitContext();
        _managementService.QueryClusterDeploymentState(s_clusterId, Arg.Any<CancellationToken>())
            .Returns(new DeploymentState(s_clusterId, "UpdatingDeployments", s_running, s_target));

        // Act
        var sut = Render(ctx);

        // Assert
        sut.WaitForAssertion(() => sut.Find(".status").TextContent.Should().Be(
            $"{Format(ClusterOverview.DeployingVersion, s_target)} · {ClusterOverview.PhaseUpdatingDeployments}"));
    }

    [Fact]
    public async Task Looks_for_a_deployment_of_the_cluster_the_page_shows_after_a_reload()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var otherClusterId = Guid.NewGuid();
        _managementService.QueryClusterDeploymentState(otherClusterId, Arg.Any<CancellationToken>())
            .Returns(new DeploymentState(otherClusterId, "UpdatingDeployments", s_running, s_target));
        var sut = Render(ctx);

        // Act
        sut.Render(p => p.Add(c => c.ClusterId, otherClusterId));

        // Assert
        sut.WaitForAssertion(() => sut.Find(".dot").ClassList.Should().Contain("status-deploying"));
    }

    [Fact]
    public async Task Leaves_out_deployments_of_another_cluster()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var sut = Render(ctx);

        // Act
        Start(new ClusterUpdateStarted(Guid.NewGuid(), Guid.NewGuid(), s_target));

        // Assert
        sut.Find(".dot").ClassList.Should().Contain("status-running");
    }

    private void Start(ClusterUpdateStarted started)
        => _managementService.OnClusterUpdateStarted += Raise.Event<EventHandler<ClusterUpdateStarted>>(_managementService, started);

    private IRenderedComponent<ClusterStatusLink> Render(BunitContext ctx, bool isRunning = true, bool isLoading = false)
    {
        ctx.Services.AddClientTimeProvider(TimeZoneInfo.Utc);
        ctx.Services.AddSingleton(_managementService);
        ctx.Services.AddSingleton(Substitute.For<ILogger<ClusterStatusLink>>());

        return ctx.Render<ClusterStatusLink>(p => p
            .Add(c => c.ClusterId, isRunning ? s_clusterId : null)
            .Add(c => c.RunningVersion, isRunning ? s_running : null)
            .Add(c => c.IsLoading, isLoading));
    }

    private static string Format(string format, params object?[] args)
        => string.Format(CultureInfo.CurrentCulture, format, args);
}
