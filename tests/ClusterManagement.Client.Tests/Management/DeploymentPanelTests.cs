using System.Globalization;
using Bunit;
using ClusterManagement.Client.Management.Components;
using ClusterManagement.Client.Management.Extensions;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Sdk.Messaging;
using ViciOne.Ui.Blazor.Components.Button;

namespace ClusterManagement.Client.Tests.Management;

public class DeploymentPanelTests
{
    private static readonly Guid s_clusterId = Guid.NewGuid();
    private static readonly Guid s_engineHostId = Guid.NewGuid();
    private static readonly Version s_targetVersion = new(0, 1, 2);
    private static readonly ClusterInfoModel s_running = new(new ClusterInfo(s_clusterId, new Version(0, 1, 1), "Cluster", IsActive: true));

    [Fact]
    public async Task Lists_a_failed_instance_with_its_error_but_not_its_steps_in_the_standard_view()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddClientTimeProvider();
        var (deployment, instanceIds) = Deployment(instanceCount: 2);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Failed) with { Error = Failure(instanceIds[0]).Error }, DateTimeOffset.UnixEpoch);
        deployment.Apply(Failure(instanceIds[0]));

        // Act
        var sut = ctx.Render<DeploymentPanel>(p => p.Add(c => c.Deployment, deployment).Add(c => c.Running, s_running));

        // Assert
        sut.FindAll(".instance").Should().ContainSingle();
        sut.Find(".instance .bar").ClassList.Should().Contain("failed");
        sut.Find(".instance .error").TextContent.Should().EndWith("Engine start failed");
        sut.FindAll(".steps-toggle").Should().BeEmpty();
        sut.FindAll(".status-line button").Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Offers_the_deployment_state_only_in_the_expert_view(bool expert)
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddClientTimeProvider();

        // Act
        var sut = ctx.Render<DeploymentPanel>(p => p.Add(c => c.Expert, expert).Add(c => c.MayReset, true).Add(c => c.Running, s_running));

        // Assert
        var titles = sut.FindComponents<Button>().Select(b => b.Instance.Title);
        if (expert)
            titles.Should().Equal(ClusterOverview.ShowSagaState, ClusterOverview.ResetSagaState);
        else
            titles.Should().BeEmpty();
    }

    [Fact]
    public async Task Offers_the_reset_only_with_the_right_for_it()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddClientTimeProvider();

        // Act
        var sut = ctx.Render<DeploymentPanel>(p => p.Add(c => c.Expert, true).Add(c => c.MayReset, false).Add(c => c.Running, s_running));

        // Assert
        sut.FindComponents<Button>().Select(b => b.Instance.Title).Should().Equal(ClusterOverview.ShowSagaState);
    }

    [Fact]
    public async Task Leaves_out_a_node_named_after_its_instance()
    {
        // Arrange
        await using var ctx = new BunitContext();
        ctx.Services.AddClientTimeProvider();
        var instanceId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var deployment = DeploymentProgressModel.Start(new ClusterUpdateStarted(Guid.NewGuid(), s_clusterId, s_targetVersion)
        {
            NodeApplications = new Dictionary<Guid, Guid?> { [nodeId] = instanceId },
            Nodes = new Dictionary<Guid, string> { [nodeId] = "IFM10N34571 (Physical)" },
            Applications = new Dictionary<Guid, string> { [instanceId] = "IFM10N34571" },
        }, DateTimeOffset.UnixEpoch);
        deployment.Apply(Failure(instanceId));

        // Act
        var sut = ctx.Render<DeploymentPanel>(p => p.Add(c => c.Deployment, deployment).Add(c => c.Running, s_running));

        // Assert
        sut.Find(".instance .node").TextContent.Should().BeEmpty();
    }

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
        var sut = ctx.Render<DeploymentPanel>(p => p.Add(c => c.Expert, true)
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
    public async Task Without_a_plan_shows_what_an_instance_is_busy_with_and_how_long_its_step_has_been_running()
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
            sut.Find(".instance .summary .text").TextContent.Should().Be(ClusterOverview.BusyUpdatingEngineHosts);
            sut.Find(".instance .summary .time").TextContent.Should().Be("0:37");
            sut.Find(".instance .bar").ClassList.Should().Contain("waiting");
        });
    }

    [Fact]
    public async Task With_a_plan_shows_how_far_the_instance_got_in_percent_and_on_the_bar()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var clock = ClientClock(ctx);
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        deployment.Apply(Planned(deployment, instanceIds[0], ClusterNodeDeploymentStep.DeployEngineHost, ClusterNodeDeploymentStep.StartEngineHost,
            ClusterNodeDeploymentStep.QueryEngineStates, ClusterNodeDeploymentStep.DeployEngine));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.DeployEngineHost), clock.GetUtcNow());
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.DeployEngineHost),
            clock.GetUtcNow().AddSeconds(2));

        // Act
        var sut = RenderWith(ctx, deployment);

        // Assert
        sut.Find(".instance .summary .text").TextContent.Should().Be(ClusterOverview.BusyUpdatingEngineHosts);
        sut.Find(".instance .summary .time").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.Percent, 25));
        sut.Find(".instance .bar").ClassList.Should().Contain("running");
        sut.Find(".instance .bar .fill").GetAttribute("style").Should().Contain("width: 25%");
    }

    [Fact]
    public async Task Fills_the_bar_during_a_step_by_how_long_the_steps_before_took_but_not_to_its_end()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var clock = ClientClock(ctx);
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        deployment.Apply(Planned(deployment, instanceIds[0], ClusterNodeDeploymentStep.DeployEngineHost, ClusterNodeDeploymentStep.StartEngineHost));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.DeployEngineHost), clock.GetUtcNow());
        clock.Advance(TimeSpan.FromSeconds(2));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.DeployEngineHost), clock.GetUtcNow());
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started), clock.GetUtcNow());
        var sut = RenderWith(ctx, deployment);

        // Act
        clock.Advance(TimeSpan.FromSeconds(1));

        // Assert
        sut.WaitForAssertion(() => sut.Find(".instance .bar .fill").GetAttribute("style").Should().Contain("width: 75%"));
        clock.Advance(TimeSpan.FromSeconds(10));
        sut.WaitForAssertion(() => sut.Find(".instance .bar .fill").GetAttribute("style").Should().Contain("width: 95%"));
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
        sut.FindAll(".instance .bar").Should().ContainSingle();
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
    public async Task Shows_the_planned_steps_an_instance_has_not_reached_as_pending()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        deployment.Apply(Planned(deployment, instanceIds[0], ClusterNodeDeploymentStep.DeployEngineHost, ClusterNodeDeploymentStep.StartEngineHost,
            ClusterNodeDeploymentStep.QueryEngineStates));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.DeployEngineHost), DateTimeOffset.UnixEpoch);
        var sut = Render(ctx, deployment);

        // Act
        await sut.Find(".steps-toggle").ClickAsync(new());

        // Assert
        sut.Find(".instance .bar").ClassList.Should().Contain("running");
        var pending = sut.FindAll(".instance .steps .entry.pending");
        pending.Select(e => e.QuerySelector(".text")!.TextContent).Should().Equal(
            string.Format(CultureInfo.CurrentCulture, ClusterOverview.StepStartEngineHost, "Packaging"),
            string.Format(CultureInfo.CurrentCulture, ClusterOverview.StepQueryEngineStates, "Packaging"));
        pending.Should().OnlyContain(e => e.QuerySelector(".visually-hidden")!.TextContent == ClusterOverview.StepStatePending
            && e.QuerySelector(".time")!.TextContent.Length == 0);
    }

    [Fact]
    public async Task Leaves_out_the_planned_steps_once_the_deployment_ended()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        deployment.Apply(Planned(deployment, instanceIds[0], ClusterNodeDeploymentStep.StartEngineHost, ClusterNodeDeploymentStep.QueryEngineStates));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded), DateTimeOffset.UnixEpoch);

        // Act
        deployment.Complete(new ClusterUpdateCompleted(deployment.CorrelationId, s_clusterId, s_targetVersion, TimeSpan.FromSeconds(2)));
        var sut = Render(ctx, deployment);

        // Assert
        sut.Find(".instance .bar").ClassList.Should().Contain("done");
        sut.Find(".instance .bar .fill").GetAttribute("style").Should().Contain("width: 100%");
        await sut.Find(".steps-toggle").ClickAsync(new());
        sut.FindAll(".instance .steps .entry").Should().ContainSingle();
    }

    [Fact]
    public async Task Groups_the_steps_by_the_engine_host_they_work_on()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        var otherHost = Guid.NewGuid();
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.Prepare) with { EngineHostId = null, EngineHostName = null },
            DateTimeOffset.UnixEpoch);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.Prepare) with { EngineHostId = null, EngineHostName = null },
            DateTimeOffset.UnixEpoch.AddSeconds(1));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started), DateTimeOffset.UnixEpoch.AddSeconds(1));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded), DateTimeOffset.UnixEpoch.AddSeconds(3));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started) with { EngineHostId = otherHost, EngineHostName = "Filling" },
            DateTimeOffset.UnixEpoch.AddSeconds(3));
        var sut = Render(ctx, deployment);

        // Act
        await sut.Find(".steps-toggle").ClickAsync(new());

        // Assert
        sut.FindAll(".instance .steps .group").Select(g => (g.QuerySelector(".name")!.TextContent, g.QuerySelector(".count")!.TextContent, g.QuerySelector(".time")!.TextContent))
            .Should().Equal(("CoreOS 0", "1/1", "1.0 s"), ("Packaging", "1/1", "2.0 s"), ("Filling", "0/1", ""));
    }

    [Fact]
    public async Task Marks_where_the_rollback_starts_in_the_steps()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.DeployEngineHost), DateTimeOffset.UnixEpoch);
        deployment.Apply(new ClusterUpdateChanged(deployment.CorrelationId, s_clusterId, s_targetVersion, "RollingBackDeployments") { Change = ClusterUpdateChange.Transition });
        Receive(deployment, RollbackStep(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch);
        var sut = Render(ctx, deployment);

        // Act
        await sut.Find(".steps-toggle").ClickAsync(new());

        // Assert
        sut.FindAll(".instance .steps li").Select(li => li.ClassList.Contains("divider") ? "divider" : li.ClassList.Contains("group") ? "group" : "entry")
            .Should().Equal("group", "entry", "divider", "group", "entry");
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
        Receive(deployment, RollbackStep(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch.AddSeconds(10));
        Receive(deployment, RollbackStep(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch.AddSeconds(11));
        Receive(deployment, RollbackStep(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.StopEngineHost), DateTimeOffset.UnixEpoch.AddSeconds(11));
        Receive(deployment, RollbackStep(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.StopEngineHost), DateTimeOffset.UnixEpoch.AddSeconds(14.5));
        deployment.Fail(new ClusterUpdateFailed(deployment.CorrelationId, s_clusterId, s_targetVersion) { State = "RollingBackDeployments" });

        // Act
        var sut = Render(ctx, deployment);

        // Assert
        sut.Find(".instance .chip").TextContent.Should().Be(ClusterOverview.InstanceRolledBack);
        sut.Find(".instance .summary .text").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.StepsRolledBack, 2));
        sut.Find(".instance .summary .time").TextContent.Should().Be("4.5 s");
    }

    [Fact]
    public async Task Runs_the_rollback_of_a_failed_instance_in_the_rollback_color()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Failed, ClusterNodeDeploymentStep.DeployEngineHost) with { Error = Failure(instanceIds[0]).Error },
            DateTimeOffset.UnixEpoch);
        deployment.Apply(Failure(instanceIds[0]));
        deployment.Apply(new ClusterUpdateChanged(deployment.CorrelationId, s_clusterId, s_targetVersion, "RollingBackDeployments") { Change = ClusterUpdateChange.Transition });

        // Act
        Receive(deployment, RollbackStep(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch);
        var sut = Render(ctx, deployment);

        // Assert
        sut.Find(".instance .bar").ClassList.Should().Contain("rollback").And.NotContain("failed");
    }

    [Theory]
    [InlineData(false, "rolled-back")]
    [InlineData(true, "failed")]
    public async Task Ends_a_rolled_back_instance_in_the_rollback_color_unless_the_rollback_failed(bool rollbackFailed, string barClass)
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch.AddSeconds(1));
        Receive(deployment, RollbackStep(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch.AddSeconds(5));

        // Act
        deployment.Fail(new ClusterUpdateFailed(deployment.CorrelationId, s_clusterId, s_targetVersion)
        {
            State = "RollingBackDeployments",
            ErrorsByApplication = rollbackFailed ? new() { [instanceIds[0]] = new ErrorInfo(204, "Could not stop engine host") } : [],
        });
        var sut = Render(ctx, deployment);

        // Assert
        sut.Find(".instance .bar").ClassList.Should().Contain(barClass);
        sut.Find(".instance .bar .fill").GetAttribute("style").Should().Contain("width: 100%");
    }

    [Fact]
    public async Task Counts_the_planned_steps_of_an_updated_instance_and_leaves_out_a_duration_the_page_saw_only_part_of()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        deployment.Apply(Planned(deployment, instanceIds[0], ClusterNodeDeploymentStep.DeployEngineHost, ClusterNodeDeploymentStep.StartEngineHost,
            ClusterNodeDeploymentStep.QueryEngineStates));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.QueryEngineStates), DateTimeOffset.UnixEpoch);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.QueryEngineStates), DateTimeOffset.UnixEpoch.AddSeconds(1));

        // Act
        deployment.Apply(new ClusterUpdateChanged(deployment.CorrelationId, s_clusterId, s_targetVersion, "UpdatingDeployments") { Change = ClusterUpdateChange.Update, ApplicationId = instanceIds[0] });
        var sut = Render(ctx, deployment);

        // Assert
        sut.Find(".instance .summary .text").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.StepsUpdated, 3));
        sut.Find(".instance .summary .time").TextContent.Should().BeEmpty();
    }

    [Fact]
    public async Task Leaves_out_the_duration_of_a_run_whose_first_start_the_page_did_not_see()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch);
        deployment.Apply(Planned(deployment, instanceIds[0], ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStep.QueryEngineStates));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.QueryEngineStates), DateTimeOffset.UnixEpoch.AddSeconds(1));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.QueryEngineStates), DateTimeOffset.UnixEpoch.AddSeconds(2));

        // Act
        deployment.Apply(new ClusterUpdateChanged(deployment.CorrelationId, s_clusterId, s_targetVersion, "UpdatingDeployments") { Change = ClusterUpdateChange.Update, ApplicationId = instanceIds[0] });
        var sut = Render(ctx, deployment);

        // Assert
        sut.Find(".instance .summary .time").TextContent.Should().BeEmpty();
    }

    [Fact]
    public async Task Ends_only_the_instance_whose_rollback_failed_in_the_failure_color()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 2);
        foreach (var instanceId in instanceIds)
        {
            Receive(deployment, Step(deployment, instanceId, ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch);
            Receive(deployment, Step(deployment, instanceId, ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch.AddSeconds(1));
            Receive(deployment, RollbackStep(deployment, instanceId, ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch.AddSeconds(5));
        }

        // Act
        deployment.Fail(new ClusterUpdateFailed(deployment.CorrelationId, s_clusterId, s_targetVersion)
        {
            State = "RollingBackDeployments",
            ErrorsByApplication = new() { [instanceIds[0]] = new ErrorInfo(204, "Could not stop engine host") },
        });
        var sut = Render(ctx, deployment);

        // Assert
        sut.FindAll(".instance .bar").Select(b => b.ClassList.Contains("failed") ? "failed" : b.ClassList.Contains("rolled-back") ? "rolled-back" : "other")
            .Should().Equal("failed", "rolled-back");
    }

    [Fact]
    public async Task Ends_a_rollback_the_reset_stopped_in_the_failure_color()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch.AddSeconds(1));
        Receive(deployment, RollbackStep(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.Prepare), DateTimeOffset.UnixEpoch.AddSeconds(5));

        // Act
        deployment.Fail(new ClusterUpdateFailed(deployment.CorrelationId, s_clusterId, s_targetVersion) { State = "RollingBackDeployments", WasReset = true });
        var sut = Render(ctx, deployment);

        // Assert
        sut.Find(".instance .bar").ClassList.Should().Contain("failed");
    }

    [Fact]
    public async Task Keeps_the_steps_and_the_duration_of_a_run_that_skipped_a_planned_step()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        deployment.Apply(Planned(deployment, instanceIds[0], ClusterNodeDeploymentStep.DeployEngineHost, ClusterNodeDeploymentStep.StartEngineHost,
            ClusterNodeDeploymentStep.QueryEngineStates));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.DeployEngineHost), DateTimeOffset.UnixEpoch);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.DeployEngineHost), DateTimeOffset.UnixEpoch.AddSeconds(1));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started, ClusterNodeDeploymentStep.QueryEngineStates), DateTimeOffset.UnixEpoch.AddSeconds(1));
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded, ClusterNodeDeploymentStep.QueryEngineStates), DateTimeOffset.UnixEpoch.AddSeconds(3));

        // Act
        deployment.Apply(new ClusterUpdateChanged(deployment.CorrelationId, s_clusterId, s_targetVersion, "UpdatingDeployments") { Change = ClusterUpdateChange.Update, ApplicationId = instanceIds[0] });
        var sut = Render(ctx, deployment);

        // Assert
        sut.Find(".instance .summary .text").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.StepsUpdated, 2));
        sut.Find(".instance .summary .time").TextContent.Should().Be("3.0 s");
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
    public async Task Lists_the_only_instance_from_the_start()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, _) = Deployment(instanceCount: 1);

        // Act
        var sut = Render(ctx, deployment);

        // Assert
        sut.Find(".instance .chip").TextContent.Should().Be(ClusterOverview.InstanceWaiting);
        sut.FindAll(".status-line button").Should().BeEmpty();
    }

    [Fact]
    public async Task Keeps_listing_an_instance_after_the_deployment_ended()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 2);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started), DateTimeOffset.UnixEpoch);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Succeeded), DateTimeOffset.UnixEpoch.AddSeconds(1));
        var sut = Render(ctx, deployment);

        // Act
        deployment.Complete(new ClusterUpdateCompleted(deployment.CorrelationId, s_clusterId, s_targetVersion, TimeSpan.FromSeconds(2)));
        sut.Render();

        // Assert
        sut.Instance.VisibleInstances.Select(i => i.Id).Should().Equal(instanceIds[0]);
    }

    [Fact]
    public async Task An_instance_that_reported_a_step_has_started()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var (deployment, instanceIds) = Deployment(instanceCount: 1);
        Receive(deployment, Step(deployment, instanceIds[0], ClusterNodeDeploymentStepStatus.Started), DateTimeOffset.UnixEpoch);

        // Act
        var sut = Render(ctx, deployment);

        // Assert
        sut.Find(".instance .chip").TextContent.Should().Be(ClusterOverview.InstanceStarted);
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
        sut.FindAll(".instance .summary .text").Should().BeEmpty();
        sut.FindAll(".instance .summary .time").Should().BeEmpty();
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

    [Fact]
    public async Task Says_where_a_reset_deployment_hung()
    {
        // Arrange
        await using var ctx = new BunitContext();
        var deployment = DeploymentProgressModel.Resume(new DeploymentState(s_clusterId, "RollingBackCommit", s_running.Version, s_targetVersion), null);
        deployment.Fail(new ClusterUpdateFailed(Guid.NewGuid(), s_clusterId, s_targetVersion)
        {
            State = "RollingBackCommit",
            Reason = "The deployment state was reset",
            WasReset = true,
            Duration = TimeSpan.FromSeconds(42),
        });

        // Act
        var sut = Render(ctx, deployment);

        // Assert
        sut.Find(".status-line .muted").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.ResetInPhase,
            DeploymentPhase.RollingBack.ToText(), TimeSpan.FromSeconds(42).ToDisplayText(CultureInfo.CurrentCulture)));
    }

    private static FakeTimeProvider ClientClock(BunitContext ctx)
    {
        FakeTimeProvider clock = new(DateTimeOffset.UnixEpoch);
        ctx.Services.AddKeyedSingleton<TimeProvider>(Sdk.Constants.ClientTimeProviderServiceKey, clock);
        return clock;
    }

    private static IRenderedComponent<DeploymentPanel> RenderWith(BunitContext ctx, DeploymentProgressModel deployment)
        => ctx.Render<DeploymentPanel>(p => p.Add(c => c.Expert, true)
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

    private static ClusterNodeDeploymentPlanned Planned(DeploymentProgressModel deployment, Guid instanceId, params ClusterNodeDeploymentStep[] steps)
        => new(deployment.CorrelationId, s_clusterId, instanceId,
            [.. steps.Select(step => new ClusterNodeDeploymentPlannedStep(step) { EngineHostId = s_engineHostId, EngineHostName = "Packaging" })]);

    private static ClusterNodeDeploymentStepChanged RollbackStep(DeploymentProgressModel deployment, Guid instanceId, ClusterNodeDeploymentStepStatus status,
        ClusterNodeDeploymentStep step = ClusterNodeDeploymentStep.StartEngineHost)
        => Step(deployment, instanceId, status, step) with { IsRollback = true };

    // On an instance whose clock agrees with the client's, so the steps take as long as their events lie apart.
    private static void Receive(DeploymentProgressModel deployment, ClusterNodeDeploymentStepChanged step, DateTimeOffset receivedAt)
        => deployment.Apply(step with { OccurredAt = receivedAt }, receivedAt);

    private static IRenderedComponent<DeploymentPanel> Render(BunitContext ctx, DeploymentProgressModel? deployment)
    {
        ctx.Services.AddClientTimeProvider();

        return ctx.Render<DeploymentPanel>(p => p.Add(c => c.Expert, true)
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
