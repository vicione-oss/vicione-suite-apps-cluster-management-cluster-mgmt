using System.Globalization;
using Bunit;
using ClusterManagement.Client.Management.Components;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Sdk.Messaging;

namespace ClusterManagement.Client.Tests.Management;

public class DeploymentPanelTests
{
    private static readonly Guid s_clusterId = Guid.NewGuid();
    private static readonly Version s_targetVersion = new(0, 1, 2);
    private static readonly ClusterInfoModel s_running = new(new ClusterInfo(s_clusterId, new Version(0, 1, 1), "Cluster", IsActive: true));

    [Fact]
    public async Task Lists_only_the_failed_instances_of_a_deployment()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 12);
        foreach (var failed in instanceIds.TakeLast(2))
            deployment.Apply(Failure(failed));

        // Act
        var sut = Render(ctx, deployment);

        // Assert
        sut.Instance.VisibleInstances.Select(i => i.Id).Should().BeEquivalentTo(instanceIds.TakeLast(2));
    }

    [Fact]
    public async Task Show_all_lists_every_instance()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, _) = Deployment(instanceCount: 3);
        var sut = Render(ctx, deployment);

        // Act
        await sut.Find(".status-line button").ClickAsync(new());

        // Assert
        sut.Instance.VisibleInstances.Should().HaveCount(3);
    }

    [Fact]
    public async Task Without_a_deployment_the_running_version_is_shown_as_completed()
    {
        // Arrange
        await using var ctx = new BunitContext();

        // Act
        var sut = Render(ctx, null);

        // Assert
        sut.Find(".headline").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.RunningVersionDeployed, s_running.Version));
        sut.FindAll(".flow-step.done").Should().HaveCount(5);
        sut.FindAll(".status-line button").Should().BeEmpty();
    }

    [Fact]
    public async Task Shows_the_start_time_in_the_users_time_zone()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddClientTimeProvider(TimeZoneInfo.CreateCustomTimeZone("UTC+05", TimeSpan.FromHours(5), "UTC+05", "UTC+05"));
        var (deployment, _) = Deployment(instanceCount: 1);

        // Act
        var sut = ctx.Render<DeploymentPanel>(p => p
            .Add(c => c.Deployment, deployment)
            .Add(c => c.Running, s_running));

        // Assert
        sut.Find(".panel-head .muted").TextContent.Should().Contain("05:00:00");
    }

    private static IRenderedComponent<DeploymentPanel> Render(BunitContext ctx, DeploymentProgressModel? deployment)
    {
        ctx.Services.AddClientTimeProvider();

        return ctx.Render<DeploymentPanel>(p => p
            .Add(c => c.Deployment, deployment)
            .Add(c => c.Running, s_running));
    }

    private static ClusterUpdateChanged Failure(Guid instanceId)
        => new(Guid.NewGuid(), s_clusterId, s_targetVersion, "UpdatingDeployments")
        {
            Change = ClusterUpdateChange.Update,
            ApplicationId = instanceId,
            Error = new ErrorInfo(201, "Engine start failed"),
        };

    private static (DeploymentProgressModel Deployment, List<Guid> InstanceIds) Deployment(int instanceCount)
    {
        var nodes = Enumerable.Range(0, instanceCount).Select(_ => Guid.NewGuid()).ToList();
        var instances = nodes.Select(_ => Guid.NewGuid()).ToList();
        var started = new ClusterUpdateStarted(Guid.NewGuid(), s_clusterId, s_targetVersion)
        {
            NodeApplications = nodes.Zip(instances).ToDictionary(k => k.First, k => (Guid?)k.Second),
            Nodes = nodes.Select((id, i) => (id, i)).ToDictionary(k => k.id, k => $"node-{k.i}"),
            Applications = instances.Select((id, i) => (id, i)).ToDictionary(k => k.id, k => $"CoreOS {k.i}"),
        };

        return (DeploymentProgressModel.Start(started, DateTimeOffset.UnixEpoch), instances);
    }
}
