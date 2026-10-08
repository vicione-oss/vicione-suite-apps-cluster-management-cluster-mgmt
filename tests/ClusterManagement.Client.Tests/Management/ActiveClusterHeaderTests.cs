using System.Globalization;
using Bunit;
using ClusterManagement.Client.Management.Components;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Public.Events;
using ViciOne.Ui.Blazor.Components.Button;
using ViciOne.Ui.Blazor.Components.TestingHelpers.Button.Extensions;

namespace ClusterManagement.Client.Tests.Management;

public class ActiveClusterHeaderTests
{
    [Fact]
    public async Task Shows_the_deployment_time_in_the_users_time_zone()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddClientTimeProvider(TimeZoneInfo.CreateCustomTimeZone("UTC+05", TimeSpan.FromHours(5), "UTC+05", "UTC+05"));
        var active = ClusterInfoModels.Version("0.1.1", isActive: true, deployed: new DateTimeOffset(2026, 10, 4, 10, 0, 0, TimeSpan.Zero));

        // Act
        var sut = ctx.Render<ActiveClusterHeader>(p => p.Add(c => c.Active, active));

        // Assert
        sut.Find(".facts").TextContent.Should().Contain("15:00:00");
    }

    [Fact]
    public async Task Deploy_is_disabled_without_a_draft()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddClientTimeProvider();

        // Act
        var sut = ctx.Render<ActiveClusterHeader>(p => p.Add(c => c.Active, ClusterInfoModels.Version("0.1.1", isActive: true)));

        // Assert
        DeployButton(sut).Instance.Enabled.Should().BeFalse();
        DeployButton(sut).Instance.Title.Should().Be(ClusterOverview.NothingToDeploy);
    }

    [Fact]
    public async Task Deploy_is_disabled_while_a_deployment_runs()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddClientTimeProvider();
        var deployment = DeploymentProgressModel.Start(new ClusterUpdateStarted(Guid.NewGuid(), ClusterInfoModels.ClusterId, new Version(0, 1, 2)), DateTimeOffset.UnixEpoch);

        // Act
        var sut = ctx.Render<ActiveClusterHeader>(p => p
            .Add(c => c.Active, ClusterInfoModels.Version("0.1.1", isActive: true))
            .Add(c => c.Draft, ClusterInfoModels.Version("0.1.2"))
            .Add(c => c.Deployment, deployment));

        // Assert
        DeployButton(sut).Instance.Enabled.Should().BeFalse();
        sut.Find(".chip.status-deploying").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.DeployingVersion, deployment.TargetVersion));
    }

    [Fact]
    public async Task Deploy_sends_the_draft()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddClientTimeProvider();
        var deployed = false;
        var sut = ctx.Render<ActiveClusterHeader>(p => p
            .Add(c => c.Active, ClusterInfoModels.Version("0.1.1", isActive: true))
            .Add(c => c.Draft, ClusterInfoModels.Version("0.1.2"))
            .Add(c => c.OnDeployDraft, () => deployed = true));

        // Act
        await DeployButton(sut).FindInnerButton().ClickAsync(new());

        // Assert
        deployed.Should().BeTrue();
    }

    private static IRenderedComponent<Button> DeployButton(IRenderedComponent<ActiveClusterHeader> sut)
        => sut.FindComponents<Button>().Single(b => b.Instance.CssClass == "cm-primary");
}
