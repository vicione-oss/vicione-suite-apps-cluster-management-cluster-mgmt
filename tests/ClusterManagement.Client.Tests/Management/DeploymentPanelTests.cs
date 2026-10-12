using System.Globalization;
using Bunit;
using ClusterManagement.Client.Management.Components;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Sdk.Messaging;

namespace ClusterManagement.Client.Tests.Management;

public class DeploymentPanelTests
{
    private static readonly Guid s_clusterId = Guid.NewGuid();
    private static readonly Guid s_engineHostId = Guid.NewGuid();
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
    public async Task Offers_no_toggle_when_every_instance_is_listed_already()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        deployment.Apply(Failure(instanceIds[0]));

        // Act
        var sut = Render(ctx, deployment);

        // Assert
        sut.FindAll(".instance").Should().ContainSingle();
        sut.FindAll(".status-line button").Should().BeEmpty();
    }

    [Fact]
    public async Task Offers_the_toggle_while_an_instance_is_not_listed()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 2);
        deployment.Apply(Failure(instanceIds[0]));

        // Act
        var sut = Render(ctx, deployment);

        // Assert
        sut.FindAll(".status-line button").Should().ContainSingle();
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

    [Fact]
    public async Task Lists_an_instance_while_it_works_on_a_step()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 3);
        Receive(deployment, Step(deployment, instanceIds[1], ClusterNodeDeploymentStepStatus.Started), DateTimeOffset.UnixEpoch);

        // Act
        var sut = Render(ctx, deployment);

        // Assert
        sut.Instance.VisibleInstances.Select(i => i.Id).Should().Equal(instanceIds[1]);
    }

    [Fact]
    public async Task Keeps_listing_an_instance_between_two_steps()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 3);
        Receive(deployment, Step(deployment, instanceIds[1], ClusterNodeDeploymentStepStatus.Started), DateTimeOffset.UnixEpoch);
        Receive(deployment, Step(deployment, instanceIds[1], ClusterNodeDeploymentStepStatus.Succeeded), DateTimeOffset.UnixEpoch.AddSeconds(1));

        // Act
        var sut = Render(ctx, deployment);

        // Assert
        sut.Instance.VisibleInstances.Select(i => i.Id).Should().Equal(instanceIds[1]);
    }

    [Fact]
    public async Task Shows_the_step_of_an_instance_and_how_long_it_has_been_running()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var clock = ClientClock(ctx);
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started), clock.GetUtcNow());
        var sut = RenderWith(ctx, deployment);

        // Act
        clock.Advance(TimeSpan.FromSeconds(37));

        // Assert
        sut.WaitForAssertion(() =>
        {
            sut.Find(".instance .summary .text").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.StepStartEngineHost, "Packaging"));
            sut.Find(".instance .summary .time").TextContent.Should().Be("0:37");
        });
    }

    [Fact]
    public async Task Lists_the_steps_of_an_instance_collapsed()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.DeployEngineHost), DateTimeOffset.UnixEpoch);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.DeployEngineHost), DateTimeOffset.UnixEpoch);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started), DateTimeOffset.UnixEpoch);

        // Act
        var sut = Render(ctx, deployment);

        // Assert
        sut.FindAll(".instance .trail .segment").Select(s => s.ClassList.Contains("done") ? "done" : s.ClassList.Contains("current") ? "current" : "other")
            .Should().Equal("done", "current");
        sut.Find(".steps-toggle").GetAttribute("aria-expanded").Should().Be("false");
        sut.FindAll(".instance .steps").Should().BeEmpty();
    }

    [Fact]
    public async Task Click_on_an_instance_shows_its_steps_and_a_second_click_hides_them()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.DeployEngineHost), DateTimeOffset.UnixEpoch);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.DeployEngineHost), DateTimeOffset.UnixEpoch.AddSeconds(2));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started), DateTimeOffset.UnixEpoch.AddSeconds(2));
        var sut = Render(ctx, deployment);

        // Act
        await sut.Find(".steps-toggle").ClickAsync(new());

        // Assert
        sut.Find(".steps-toggle").GetAttribute("aria-expanded").Should().Be("true");
        sut.FindAll(".instance .steps .entry .text").Select(t => t.TextContent).Should().Equal(
            string.Format(CultureInfo.CurrentCulture, ClusterOverview.StepDeployEngineHost, "Packaging"),
            string.Format(CultureInfo.CurrentCulture, ClusterOverview.StepStartEngineHost, "Packaging"));
        sut.Find(".instance .steps .entry .time").TextContent.Should().Be("2.0 s");

        await sut.Find(".steps-toggle").ClickAsync(new());

        sut.FindAll(".instance .steps").Should().BeEmpty();
    }

    [Fact]
    public async Task Marks_where_the_rollback_starts_in_the_steps()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.DeployEngineHost), DateTimeOffset.UnixEpoch);
        deployment.Apply(new ClusterUpdateChanged(deployment.CorrelationId, s_clusterId, s_targetVersion, "RollingBackDeployments") { Change = ClusterUpdateChange.Transition });
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch);
        var sut = Render(ctx, deployment);

        // Act
        await sut.Find(".steps-toggle").ClickAsync(new());

        // Assert
        sut.FindAll(".instance .steps li").Select(li => li.ClassList.Contains("divider") ? "divider" : "entry")
            .Should().Equal("entry", "divider", "entry");
    }

    [Fact]
    public async Task Shows_how_long_an_updated_instance_took()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started), DateTimeOffset.UnixEpoch);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded), DateTimeOffset.UnixEpoch.AddSeconds(6.8));
        deployment.Apply(new ClusterUpdateChanged(deployment.CorrelationId, s_clusterId, s_targetVersion, "UpdatingDeployments") { Change = ClusterUpdateChange.Update, ApplicationId = instanceIds[0] });

        // Act
        var sut = Render(ctx, deployment);

        // Assert
        sut.Find(".instance .summary .text").TextContent.Should().Be(ClusterOverview.StepsUpdatedOne);
        sut.Find(".instance .summary .time").TextContent.Should().Be("6.8 s");
    }

    [Fact]
    public async Task Shows_how_long_the_rollback_of_an_instance_took_once_the_deployment_ended()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch.AddSeconds(1));
        deployment.Apply(new ClusterUpdateChanged(deployment.CorrelationId, s_clusterId, s_targetVersion, "UpdatingDeployments") { Change = ClusterUpdateChange.Update, ApplicationId = instanceIds[0] });
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch.AddSeconds(10));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch.AddSeconds(11));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.StopEngineHost), DateTimeOffset.UnixEpoch.AddSeconds(11));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.StopEngineHost), DateTimeOffset.UnixEpoch.AddSeconds(14.5));
        deployment.Fail(new ClusterUpdateFailed(deployment.CorrelationId, s_clusterId, s_targetVersion) { State = "RollingBackDeployments" });
        var sut = Render(ctx, deployment);

        // Act
        await sut.Find(".status-line button").ClickAsync(new());

        // Assert
        sut.Find(".instance .chip").TextContent.Should().Be(ClusterOverview.InstanceRolledBack);
        sut.Find(".instance .summary .text").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.StepsRolledBack, 2));
        sut.Find(".instance .summary .time").TextContent.Should().Be("4.5 s");
    }

    [Fact]
    public async Task Marks_a_step_the_engine_host_refused()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.StartEngine), DateTimeOffset.UnixEpoch);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Refused, ClusterNodeDeploymentStep.StartEngine), DateTimeOffset.UnixEpoch.AddSeconds(1));
        var sut = Render(ctx, deployment);

        // Act
        await sut.Find(".steps-toggle").ClickAsync(new());

        // Assert
        var entry = sut.Find(".instance .steps .entry.refused");
        entry.QuerySelector(".mark")!.TextContent.Should().Be("!");
        entry.QuerySelector(".visually-hidden")!.TextContent.Should().Be(ClusterOverview.StepStateRefused);
    }

    [Fact]
    public async Task Stops_the_step_clock_once_the_deployment_ended()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var clock = ClientClock(ctx);
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started), clock.GetUtcNow());
        var sut = RenderWith(ctx, deployment);
        deployment.Complete(new ClusterUpdateCompleted(deployment.CorrelationId, s_clusterId, s_targetVersion, TimeSpan.FromSeconds(40)));
        sut.Render();
        var renders = sut.RenderCount;

        // Act
        clock.Advance(TimeSpan.FromSeconds(5));

        // Assert
        sut.RenderCount.Should().Be(renders);
        sut.FindAll(".instance .summary").Should().BeEmpty();
    }

    [Fact]
    public async Task Error_of_an_instance_names_the_step_it_failed_in()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Failed) with { Error = Failure(instanceIds[0]).Error }, DateTimeOffset.UnixEpoch);
        deployment.Apply(Failure(instanceIds[0]));

        // Act
        var sut = Render(ctx, deployment);

        // Assert
        sut.Find(".error").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.StepFailed,
            string.Format(CultureInfo.CurrentCulture, ClusterOverview.StepStartEngineHost, "Packaging"), "Engine start failed"));
    }

    [Fact]
    public async Task Error_of_an_instance_without_a_failed_step_shows_only_the_message()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        deployment.Apply(Failure(instanceIds[0]));

        // Act
        var sut = Render(ctx, deployment);

        // Assert
        sut.Find(".error").TextContent.Should().Be("Engine start failed");
    }

    private static FakeTimeProvider ClientClock(BunitContext ctx)
    {
        FakeTimeProvider clock = new(DateTimeOffset.UnixEpoch);
        ctx.Services.AddKeyedSingleton<TimeProvider>(Sdk.Constants.ClientTimeProviderServiceKey, clock);
        return clock;
    }

    private static IRenderedComponent<DeploymentPanel> RenderWith(BunitContext ctx, DeploymentProgressModel deployment)
        => ctx.Render<DeploymentPanel>(p => p
            .Add(c => c.Deployment, deployment)
            .Add(c => c.Running, s_running));

    private static ClusterNodeDeploymentStepChanged Step(DeploymentProgressModel deployment, Guid instanceId, ClusterNodeDeploymentStepStatus status,
        ClusterNodeDeploymentStep step = ClusterNodeDeploymentStep.StartEngineHost)
        => new(deployment.CorrelationId, s_clusterId, instanceId, step, status)
        {
            EngineHostId = s_engineHostId,
            EngineHostName = "Packaging",
            OccurredAt = DateTimeOffset.UnixEpoch,
        };

    // On an instance whose clock agrees with the client's, so the steps take as long as their events lie apart.
    private static void Receive(DeploymentProgressModel deployment, ClusterNodeDeploymentStepChanged step, DateTimeOffset receivedAt)
        => deployment.Apply(step with { OccurredAt = receivedAt }, receivedAt);

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
