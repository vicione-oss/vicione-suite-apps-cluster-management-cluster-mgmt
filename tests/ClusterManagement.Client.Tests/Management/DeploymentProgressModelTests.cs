using ClusterManagement.Client.Management.Models;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Sdk.Messaging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Tests.Management;

public class DeploymentProgressModelTests
{
    private static readonly Guid s_correlationId = Guid.NewGuid();
    private static readonly Guid s_clusterId = Guid.NewGuid();
    private static readonly Guid s_nodeId = Guid.NewGuid();
    private static readonly Guid s_instanceId = Guid.NewGuid();
    private static readonly Version s_targetVersion = new(0, 1, 2);
    private static readonly Guid s_engineHostId = Guid.NewGuid();
    private static readonly Guid s_engineId = Guid.NewGuid();
    private static readonly DateTimeOffset s_receivedAt = DateTimeOffset.UnixEpoch.AddMinutes(3);
    private static readonly ErrorInfo s_updateError = new(207, "Could not deploy engine in engine host");
    private static readonly ErrorInfo s_rollbackError = new(204, "Could not stop engine host");

    [Fact]
    public void Starts_with_one_entry_per_instance()
    {
        // Act
        var progress = Start();

        // Assert
        progress.Phase.Should().Be(DeploymentPhase.Starting);
        progress.IsRunning.Should().BeTrue();
        progress.Instances.Should().ContainSingle()
            .Which.Should().Match<InstanceProgressModel>(i => i.Name == "CoreOS" && i.NodeName == "edge-1");
    }

    [Theory]
    [InlineData("CommittingCluster", DeploymentPhase.Committing)]
    [InlineData("AddingClusterNodes", DeploymentPhase.AddingNodes)]
    [InlineData("UpdatingDeployments", DeploymentPhase.UpdatingDeployments)]
    [InlineData("CleaningClusterNodes", DeploymentPhase.CleaningUp)]
    [InlineData("CleaningCluster", DeploymentPhase.CleaningUp)]
    [InlineData("RollingBackDeployments", DeploymentPhase.RollingBack)]
    [InlineData("RollingBackCommit", DeploymentPhase.RollingBack)]
    [InlineData("RollingBackClusterNodes", DeploymentPhase.RollingBack)]
    public void Transition_enters_the_matching_phase(string sagaState, DeploymentPhase expected)
    {
        // Arrange
        var progress = Start();

        // Act
        progress.Apply(Transition(sagaState));

        // Assert
        progress.Phase.Should().Be(expected);
    }

    [Fact]
    public void Transition_to_an_unknown_state_keeps_the_phase()
    {
        // Arrange
        var progress = Start();
        progress.Apply(Transition("UpdatingDeployments"));

        // Act
        progress.Apply(Transition("Final"));

        // Assert
        progress.Phase.Should().Be(DeploymentPhase.UpdatingDeployments);
    }

    [Fact]
    public void Update_of_an_instance_marks_it_updated()
    {
        // Arrange
        var progress = Start();

        // Act
        progress.Apply(InstanceChange(ClusterUpdateChange.Update));

        // Assert
        progress.Instances[0].IsUpdated.Should().BeTrue();
        progress.Instances[0].HasFailed.Should().BeFalse();
    }

    [Fact]
    public void Update_with_an_error_marks_the_instance_failed()
    {
        // Arrange
        var progress = Start();

        // Act
        progress.Apply(InstanceChange(ClusterUpdateChange.Update, new ErrorInfo(201, "Engine start failed")));

        // Assert
        progress.Instances[0].IsUpdated.Should().BeFalse();
        progress.Instances[0].Error!.Message.Should().Be("Engine start failed");
    }

    [Fact]
    public void Error_after_an_update_takes_back_the_update_of_the_instance()
    {
        // Arrange
        var progress = Start();
        progress.Apply(InstanceChange(ClusterUpdateChange.Update));

        // Act
        progress.Apply(InstanceChange(ClusterUpdateChange.Start, new ErrorInfo(202, "Engine host did not start")));

        // Assert
        progress.Instances[0].IsUpdated.Should().BeFalse();
        progress.Instances[0].HasFailed.Should().BeTrue();
    }

    [Fact]
    public void Completion_stops_the_deployment_and_keeps_its_duration()
    {
        // Arrange
        var progress = Start();

        // Act
        progress.Complete(new ClusterUpdateCompleted(s_correlationId, s_clusterId, s_targetVersion, TimeSpan.FromSeconds(4)));

        // Assert
        progress.Phase.Should().Be(DeploymentPhase.Completed);
        progress.IsRunning.Should().BeFalse();
        progress.Duration.Should().Be(TimeSpan.FromSeconds(4));
    }

    [Fact]
    public void Failure_keeps_reason_duration_and_errors_per_instance()
    {
        // Arrange
        var progress = Start();
        progress.Apply(Transition("UpdatingDeployments"));

        // Act
        progress.Fail(Failed("UpdatingDeployments"));

        // Assert
        progress.Phase.Should().Be(DeploymentPhase.Failed);
        progress.FailedPhase.Should().Be(DeploymentPhase.UpdatingDeployments);
        progress.FailureReason.Should().Be("At least one application update failed");
        progress.Duration.Should().Be(TimeSpan.FromSeconds(8));
        progress.Instances[0].Error!.Message.Should().Be("Error while connecting host");
        progress.RollbackFailed.Should().BeFalse();
    }

    [Fact]
    public void Failure_during_rollback_reports_a_failed_rollback()
    {
        // Arrange
        var progress = Start();
        progress.Apply(Transition("UpdatingDeployments"));
        progress.Apply(Transition("RollingBackDeployments"));

        // Act
        progress.Fail(Failed("RollingBackDeployments"));

        // Assert
        progress.HasRolledBack.Should().BeTrue();
        progress.RollbackFailed.Should().BeTrue();
        progress.FailurePhase.Should().Be(DeploymentPhase.UpdatingDeployments);
    }

    [Fact]
    public void Failure_without_a_state_falls_back_to_the_current_phase()
    {
        // Arrange
        var progress = Start();
        progress.Apply(Transition("CommittingCluster"));

        // Act
        progress.Fail(Failed(null));

        // Assert
        progress.FailedPhase.Should().Be(DeploymentPhase.Committing);
    }

    [Fact]
    public void Failure_before_the_start_keeps_reason_and_duration_without_instances()
    {
        // Act
        var progress = DeploymentProgressModel.FailedBeforeStart(new ClusterUpdateFailed(s_correlationId, s_clusterId, s_targetVersion)
        {
            State = "Initial",
            Reason = "Cluster v0.1.2 not found.",
            Duration = TimeSpan.FromSeconds(1),
        });

        // Assert
        progress.Matches(s_clusterId, s_targetVersion).Should().BeTrue();
        progress.Phase.Should().Be(DeploymentPhase.Failed);
        progress.FailureReason.Should().Be("Cluster v0.1.2 not found.");
        progress.Duration.Should().Be(TimeSpan.FromSeconds(1));
        progress.StartedAt.Should().BeNull();
        progress.Instances.Should().BeEmpty();
    }

    [Fact]
    public void Resume_enters_the_phase_of_the_running_saga()
    {
        // Act
        var progress = DeploymentProgressModel.Resume(new DeploymentState(s_clusterId, "UpdatingDeployments", new Version(0, 1, 1), s_targetVersion), null);

        // Assert
        progress.Phase.Should().Be(DeploymentPhase.UpdatingDeployments);
        progress.StartedAt.Should().BeNull();
        progress.Instances.Should().BeEmpty();
        progress.Matches(s_clusterId, s_targetVersion).Should().BeTrue();
    }

    [Fact]
    public void Resume_lists_the_instance_of_every_node_of_the_target_version()
    {
        // Arrange
        var slaveId = Guid.NewGuid();
        var target = new Cluster
        {
            NodeGroups =
            [
                new ClusterNodeGroup
                {
                    Nodes =
                    [
                        Node("edge-1", new ClusterApplication { Id = s_instanceId, Name = "Master", Type = ClusterApplicationType.CoreOsMaster }),
                        Node("edge-2", new ClusterApplication { Id = slaveId, Name = "Slave", Type = ClusterApplicationType.CoreOsSlave }),
                        Node("plc"),
                    ],
                },
            ],
        };

        // Act
        var progress = DeploymentProgressModel.Resume(new DeploymentState(s_clusterId, "UpdatingDeployments", new Version(0, 1, 1), s_targetVersion), target);
        progress.Apply(InstanceChange(ClusterUpdateChange.Update));

        // Assert
        progress.Instances.Select(i => (i.Name, i.NodeName)).Should().Equal(("Master", "edge-1"), ("Slave", "edge-2"));
        progress.Instances.Single(i => i.Id == s_instanceId).IsUpdated.Should().BeTrue();
    }

    [Fact]
    public void Steps_mark_reached_phases_done_and_the_current_one_running()
    {
        // Arrange
        var progress = Start();
        progress.Apply(Transition("UpdatingDeployments"));

        // Act
        var steps = progress.GetSteps();

        // Assert
        steps.Select(s => s.State).Should().Equal(
            DeploymentStepState.Done, DeploymentStepState.Done, DeploymentStepState.Current, DeploymentStepState.Pending, DeploymentStepState.Pending);
    }

    [Fact]
    public void Steps_of_a_completed_deployment_are_all_done()
    {
        // Arrange
        var progress = Start();
        progress.Complete(new ClusterUpdateCompleted(s_correlationId, s_clusterId, s_targetVersion, TimeSpan.FromSeconds(4)));

        // Act
        var steps = progress.GetSteps();

        // Assert
        steps.Should().OnlyContain(s => s.State == DeploymentStepState.Done);
    }

    [Fact]
    public void A_rollback_that_ends_without_errors_restored_the_previous_version()
    {
        // Arrange
        var progress = Start();
        progress.Apply(Transition("UpdatingDeployments"));
        progress.Apply(Transition("RollingBackDeployments"));

        // Act
        progress.Fail(Failed("RollingBackDeployments") with { ErrorsByApplication = [] });

        // Assert
        progress.RollbackFailed.Should().BeFalse();
        progress.FailurePhase.Should().Be(DeploymentPhase.UpdatingDeployments);
        progress.GetSteps()[^1].Should().Be(new DeploymentStep(DeploymentPhase.RollingBack, DeploymentStepState.Done));
    }

    [Fact]
    public void A_rollback_that_could_not_delete_the_failed_version_failed()
    {
        // Arrange
        var progress = Start();
        progress.Apply(Transition("UpdatingDeployments"));
        progress.Apply(Transition("RollingBackCommit"));

        // Act
        progress.Fail(Failed("RollingBackCommit") with { ErrorsByApplication = [], FailedVersionKept = true });

        // Assert
        progress.RollbackFailed.Should().BeTrue();
        progress.GetSteps()[^1].Should().Be(new DeploymentStep(DeploymentPhase.RollingBack, DeploymentStepState.Failed));
    }

    [Fact]
    public void Steps_of_a_failed_rollback_show_the_failed_phase_and_the_failed_rollback()
    {
        // Arrange
        var progress = Start();
        progress.Apply(Transition("UpdatingDeployments"));
        progress.Apply(Transition("RollingBackDeployments"));
        progress.Fail(Failed("RollingBackDeployments"));

        // Act
        var steps = progress.GetSteps();

        // Assert
        steps.Should().HaveCount(6);
        steps[2].Should().Be(new DeploymentStep(DeploymentPhase.UpdatingDeployments, DeploymentStepState.Failed));
        steps[^1].Should().Be(new DeploymentStep(DeploymentPhase.RollingBack, DeploymentStepState.Failed));
    }

    [Fact]
    public void A_reset_marks_the_phase_the_deployment_hung_in()
    {
        // Arrange
        var progress = DeploymentProgressModel.Resume(new DeploymentState(s_clusterId, "RollingBackCommit", new Version(0, 1, 1), s_targetVersion), null);

        // Act
        progress.Fail(new ClusterUpdateFailed(s_correlationId, s_clusterId, s_targetVersion)
        {
            State = "RollingBackCommit",
            Reason = "The deployment state was reset",
            WasReset = true,
        });

        // Assert
        progress.WasReset.Should().BeTrue();
        progress.RollbackFailed.Should().BeFalse();
        progress.FailedPhase.Should().Be(DeploymentPhase.RollingBack);
        progress.GetSteps()[^1].Should().Be(new DeploymentStep(DeploymentPhase.RollingBack, DeploymentStepState.Failed));
    }

    [Fact]
    public void Steps_of_a_successful_rollback_show_it_done()
    {
        // Arrange
        var progress = Start();
        progress.Apply(Transition("UpdatingDeployments"));
        progress.Apply(Transition("RollingBackDeployments"));
        progress.Fail(Failed("UpdatingDeployments"));

        // Act
        var steps = progress.GetSteps();

        // Assert
        steps[^1].Should().Be(new DeploymentStep(DeploymentPhase.RollingBack, DeploymentStepState.Done));
    }

    [Fact]
    public void Started_step_becomes_the_current_step_of_its_instance_at_the_time_it_was_received()
    {
        // Arrange
        var progress = Start();

        // Act
        Receive(progress, HostStep(ClusterNodeDeploymentStep.StartEngineHost, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);

        // Assert
        progress.Instances.Single().CurrentStep.Should().Match<InstanceStepModel>(s =>
            s.Step == ClusterNodeDeploymentStep.StartEngineHost && s.Target == "Packaging" && s.StartedAt == s_receivedAt);
    }

    [Fact]
    public void Succeeded_step_ends_the_current_step()
    {
        // Arrange
        var progress = Start();
        Receive(progress, HostStep(ClusterNodeDeploymentStep.StartEngineHost, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);

        // Act
        Receive(progress, HostStep(ClusterNodeDeploymentStep.StartEngineHost, ClusterNodeDeploymentStepStatus.Succeeded), s_receivedAt.AddSeconds(5));

        // Assert
        progress.Instances.Single().CurrentStep.Should().BeNull();
    }

    [Fact]
    public void Succeeded_step_received_after_the_next_step_started_keeps_the_next_step()
    {
        // Arrange
        var progress = Start();
        Receive(progress, HostStep(ClusterNodeDeploymentStep.DeployEngineHost, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);
        Receive(progress, HostStep(ClusterNodeDeploymentStep.StartEngineHost, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);

        // Act
        Receive(progress, HostStep(ClusterNodeDeploymentStep.DeployEngineHost, ClusterNodeDeploymentStepStatus.Succeeded), s_receivedAt);

        // Assert
        progress.Instances.Single().CurrentStep!.Step.Should().Be(ClusterNodeDeploymentStep.StartEngineHost);
    }

    [Fact]
    public void Failed_step_ends_the_current_step()
    {
        // Arrange
        var progress = Start();
        Receive(progress, EngineStep(ClusterNodeDeploymentStep.DeployEngine, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);

        // Act
        Receive(progress, EngineStep(ClusterNodeDeploymentStep.DeployEngine, ClusterNodeDeploymentStepStatus.Failed, s_updateError), s_receivedAt.AddSeconds(2));

        // Assert
        progress.Instances.Single().CurrentStep.Should().BeNull();
    }

    [Fact]
    public void Failed_step_explains_the_error_the_instance_reports()
    {
        // Arrange
        var progress = Start();
        Receive(progress, EngineStep(ClusterNodeDeploymentStep.DeployEngine, ClusterNodeDeploymentStepStatus.Failed, s_updateError), s_receivedAt);

        // Act
        progress.Apply(InstanceChange(ClusterUpdateChange.Update, s_updateError));

        // Assert
        progress.Instances.Single().FailedStep.Should().Match<InstanceStepModel>(s => s.Step == ClusterNodeDeploymentStep.DeployEngine && s.Target == "Line 1");
    }

    [Fact]
    public void Failed_step_with_another_error_than_the_instance_reports_explains_nothing()
    {
        // Arrange
        var progress = Start();
        Receive(progress, EngineStep(ClusterNodeDeploymentStep.DeployEngine, ClusterNodeDeploymentStepStatus.Failed, s_rollbackError), s_receivedAt);

        // Act
        progress.Apply(InstanceChange(ClusterUpdateChange.Update, s_updateError));

        // Assert
        progress.Instances.Single().FailedStep.Should().BeNull();
    }

    [Fact]
    public void Engine_step_without_a_known_engine_has_no_target()
    {
        // Arrange
        var progress = Start();

        // Act
        Receive(progress, HostStep(ClusterNodeDeploymentStep.TearDownEngine, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);

        // Assert
        progress.Instances.Single().CurrentStep!.Target.Should().BeNull();
    }

    [Fact]
    public void Step_without_a_name_takes_it_from_a_known_version()
    {
        // Arrange
        var progress = Start();
        progress.KnowNamesOf(new Cluster
        {
            NodeGroups = [new ClusterNodeGroup { Nodes = [Node("edge-1", new ClusterApplication
            {
                Id = s_instanceId,
                Name = "CoreOS",
                Type = ClusterApplicationType.CoreOsMaster,
                EngineHosts = [new EngineHost { Id = s_engineHostId, Name = "Removed host" }],
            })] }],
        });

        // Act
        Receive(progress, HostStep(ClusterNodeDeploymentStep.TearDownEngineHost, ClusterNodeDeploymentStepStatus.Started) with { EngineHostName = null },
            s_receivedAt);

        // Assert
        progress.Instances.Single().CurrentStep!.Target.Should().Be("Removed host");
    }

    [Fact]
    public void Step_without_a_name_targets_the_id()
    {
        // Arrange
        var progress = Start();

        // Act
        Receive(progress, HostStep(ClusterNodeDeploymentStep.TearDownEngineHost, ClusterNodeDeploymentStepStatus.Started) with { EngineHostName = null },
            s_receivedAt);

        // Assert
        progress.Instances.Single().CurrentStep!.Target.Should().Be(s_engineHostId.ToString());
    }

    [Fact]
    public void Update_of_an_instance_ends_its_current_step()
    {
        // Arrange
        var progress = Start();
        Receive(progress, EngineStep(ClusterNodeDeploymentStep.StartEngine, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);

        // Act
        progress.Apply(InstanceChange(ClusterUpdateChange.Update));

        // Assert
        progress.Instances.Single().CurrentStep.Should().BeNull();
    }

    [Fact]
    public void Rollback_clears_the_current_steps()
    {
        // Arrange
        var progress = Start();
        progress.Apply(Transition("UpdatingDeployments"));
        Receive(progress, EngineStep(ClusterNodeDeploymentStep.StartEngine, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);

        // Act
        progress.Apply(Transition("RollingBackDeployments"));

        // Assert
        progress.Instances.Single().CurrentStep.Should().BeNull();
    }

    [Fact]
    public void Keeps_every_step_of_an_instance_with_its_duration()
    {
        // Arrange
        var progress = Start();

        // Act
        Receive(progress, HostStep(ClusterNodeDeploymentStep.DeployEngineHost, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);
        Receive(progress, HostStep(ClusterNodeDeploymentStep.DeployEngineHost, ClusterNodeDeploymentStepStatus.Succeeded), s_receivedAt.AddSeconds(3));
        Receive(progress, HostStep(ClusterNodeDeploymentStep.StartEngineHost, ClusterNodeDeploymentStepStatus.Started), s_receivedAt.AddSeconds(3));

        // Assert
        progress.Instances.Single().Steps.Select(s => (s.Step, s.State, s.Duration)).Should().Equal(
            (ClusterNodeDeploymentStep.DeployEngineHost, InstanceStepState.Succeeded, TimeSpan.FromSeconds(3)),
            (ClusterNodeDeploymentStep.StartEngineHost, InstanceStepState.Running, null));
    }

    [Fact]
    public void Duration_of_a_step_is_measured_on_the_instance_clock_however_late_its_events_arrive()
    {
        // Arrange
        var progress = Start();
        var onInstance = DateTimeOffset.UnixEpoch.AddHours(-2);
        progress.Apply(HostStep(ClusterNodeDeploymentStep.DeployEngineHost, ClusterNodeDeploymentStepStatus.Started) with { OccurredAt = onInstance }, s_receivedAt);

        // Act
        progress.Apply(HostStep(ClusterNodeDeploymentStep.DeployEngineHost, ClusterNodeDeploymentStepStatus.Succeeded) with { OccurredAt = onInstance.AddSeconds(3) },
            s_receivedAt.AddMilliseconds(2));

        // Assert
        var instance = progress.Instances.Single();
        instance.Steps.Single().Duration.Should().Be(TimeSpan.FromSeconds(3));
        instance.Duration.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void End_received_before_its_start_completes_the_step()
    {
        // Arrange
        var progress = Start();
        Receive(progress, HostStep(ClusterNodeDeploymentStep.StartEngineHost, ClusterNodeDeploymentStepStatus.Succeeded), s_receivedAt.AddSeconds(4));

        // Act
        Receive(progress, HostStep(ClusterNodeDeploymentStep.StartEngineHost, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);

        // Assert
        var instance = progress.Instances.Single();
        instance.Steps.Should().ContainSingle().Which.Should().Match<InstanceStepModel>(s =>
            s.State == InstanceStepState.Succeeded && s.Duration == TimeSpan.FromSeconds(4));
        instance.CurrentStep.Should().BeNull();
    }

    [Fact]
    public void Rollback_interrupts_the_running_step_and_marks_its_own_steps()
    {
        // Arrange
        var progress = Start();
        progress.Apply(Transition("UpdatingDeployments"));
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);
        Receive(progress, EngineStep(ClusterNodeDeploymentStep.StartEngine, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);
        progress.Apply(Transition("RollingBackDeployments"));

        // Act
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Started), s_receivedAt.AddSeconds(1));

        // Assert
        progress.Instances.Single().Steps.Select(s => (s.Step, s.State, s.IsRollback)).Should().Equal(
            (ClusterNodeDeploymentStep.Prepare, InstanceStepState.Interrupted, false),
            (ClusterNodeDeploymentStep.StartEngine, InstanceStepState.Interrupted, false),
            (ClusterNodeDeploymentStep.Prepare, InstanceStepState.Running, true));
    }

    [Fact]
    public void Rollback_step_received_before_the_rollback_phase_belongs_to_the_rollback()
    {
        // Arrange
        var progress = Start();
        progress.Apply(Transition("UpdatingDeployments"));
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Succeeded), s_receivedAt.AddSeconds(1));
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Started), s_receivedAt.AddSeconds(2));

        // Act
        progress.Apply(Transition("RollingBackDeployments"));

        // Assert
        var instance = progress.Instances.Single();
        instance.RollbackStarted.Should().BeTrue();
        instance.Steps.Select(s => (s.State, s.IsRollback)).Should().Equal(
            (InstanceStepState.Succeeded, false),
            (InstanceStepState.Running, true));
    }

    [Fact]
    public void Preparation_whose_end_arrived_before_its_start_does_not_begin_a_rollback()
    {
        // Arrange
        var progress = Start();
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Succeeded), s_receivedAt.AddSeconds(1));

        // Act
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);

        // Assert
        var instance = progress.Instances.Single();
        instance.RollbackStarted.Should().BeFalse();
        instance.Steps.Should().ContainSingle();
    }

    [Fact]
    public void First_step_seen_during_the_rollback_belongs_to_the_rollback()
    {
        // Arrange
        var progress = Start();
        progress.Apply(Transition("RollingBackDeployments"));

        // Act
        Receive(progress, HostStep(ClusterNodeDeploymentStep.StopEngineHost, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);

        // Assert
        progress.Instances.Single().Steps.Single().IsRollback.Should().BeTrue();
    }

    [Fact]
    public void Duration_of_a_rolled_back_instance_covers_only_its_rollback()
    {
        // Arrange
        var progress = Start();
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Succeeded), s_receivedAt.AddSeconds(9));
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Started), s_receivedAt.AddSeconds(20));
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Succeeded), s_receivedAt.AddSeconds(21));

        // Act
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Plan, ClusterNodeDeploymentStepStatus.Started), s_receivedAt.AddSeconds(21));
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Plan, ClusterNodeDeploymentStepStatus.Succeeded), s_receivedAt.AddSeconds(23));

        // Assert
        var instance = progress.Instances.Single();
        instance.LatestRun.Select(s => s.Step).Should().Equal(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStep.Plan);
        instance.Duration.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Steps_of_engines_with_the_same_name_on_two_engine_hosts_end_separately()
    {
        // Arrange
        var progress = Start();
        var otherHost = EngineStep(ClusterNodeDeploymentStep.StartEngine, ClusterNodeDeploymentStepStatus.Started) with
        {
            EngineHostId = Guid.NewGuid(),
            EngineId = Guid.NewGuid(),
        };
        Receive(progress, EngineStep(ClusterNodeDeploymentStep.StartEngine, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);
        Receive(progress, otherHost, s_receivedAt);

        // Act
        Receive(progress, EngineStep(ClusterNodeDeploymentStep.StartEngine, ClusterNodeDeploymentStepStatus.Succeeded), s_receivedAt.AddSeconds(1));

        // Assert
        progress.Instances.Single().Steps.Select(s => s.State).Should().Equal(InstanceStepState.Succeeded, InstanceStepState.Running);
    }

    [Fact]
    public void Step_the_engine_host_refused_ends_as_refused()
    {
        // Arrange
        var progress = Start();
        Receive(progress, EngineStep(ClusterNodeDeploymentStep.StartEngine, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);

        // Act
        Receive(progress, EngineStep(ClusterNodeDeploymentStep.StartEngine, ClusterNodeDeploymentStepStatus.Refused), s_receivedAt.AddSeconds(1));

        // Assert
        var instance = progress.Instances.Single();
        instance.Steps.Single().State.Should().Be(InstanceStepState.Refused);
        instance.CurrentStep.Should().BeNull();
    }

    [Fact]
    public void Duration_of_an_instance_spans_from_its_first_start_to_its_last_end()
    {
        // Arrange
        var progress = Start();
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Succeeded), s_receivedAt.AddSeconds(1));
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Plan, ClusterNodeDeploymentStepStatus.Started), s_receivedAt.AddSeconds(1));

        // Act
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Plan, ClusterNodeDeploymentStepStatus.Succeeded), s_receivedAt.AddSeconds(6.5));

        // Assert
        progress.Instances.Single().Duration.Should().Be(TimeSpan.FromSeconds(6.5));
    }

    [Fact]
    public void Failed_rollback_step_keeps_explaining_the_error_of_the_update_while_it_is_shown()
    {
        // Arrange
        var progress = FailedUpdateThatRollsBack();

        // Act
        Receive(progress, HostStep(ClusterNodeDeploymentStep.StopEngineHost, ClusterNodeDeploymentStepStatus.Failed, s_rollbackError), s_receivedAt);

        // Assert
        var instance = progress.Instances.Single();
        instance.FailedStep!.Step.Should().Be(ClusterNodeDeploymentStep.DeployEngine);
        instance.CurrentStep.Should().BeNull();
    }

    [Fact]
    public void Failure_with_the_error_of_the_rollback_names_the_failed_rollback_step()
    {
        // Arrange
        var progress = FailedUpdateThatRollsBack();
        Receive(progress, HostStep(ClusterNodeDeploymentStep.StopEngineHost, ClusterNodeDeploymentStepStatus.Failed, s_rollbackError), s_receivedAt);

        // Act
        progress.Fail(Failed("RollingBackDeployments") with { ErrorsByApplication = new() { [s_instanceId] = s_rollbackError } });

        // Assert
        var instance = progress.Instances.Single();
        instance.Error.Should().Be(s_rollbackError);
        instance.FailedStep!.Step.Should().Be(ClusterNodeDeploymentStep.StopEngineHost);
    }

    [Fact]
    public void Timeline_adds_the_planned_steps_the_instance_has_not_reached_as_pending()
    {
        // Arrange
        var progress = Start();
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);

        // Act
        progress.Apply(Planned(PlannedHost(ClusterNodeDeploymentStep.Prepare), PlannedHost(ClusterNodeDeploymentStep.StartEngineHost), PlannedEngine(ClusterNodeDeploymentStep.StartEngine)));

        // Assert
        progress.Instances.Single().Timeline.Select(s => (s.Step, s.State, s.Target)).Should().Equal(
            (ClusterNodeDeploymentStep.Prepare, InstanceStepState.Running, "Packaging"),
            (ClusterNodeDeploymentStep.StartEngineHost, InstanceStepState.Pending, "Packaging"),
            (ClusterNodeDeploymentStep.StartEngine, InstanceStepState.Pending, "Line 1"));
    }

    [Fact]
    public void Later_plan_of_the_same_run_replaces_the_earlier_one()
    {
        // Arrange
        var progress = Start();
        progress.Apply(Planned(PlannedHost(ClusterNodeDeploymentStep.Prepare), PlannedHost(ClusterNodeDeploymentStep.QueryEngineStates)));

        // Act
        progress.Apply(Planned(PlannedHost(ClusterNodeDeploymentStep.Prepare), PlannedHost(ClusterNodeDeploymentStep.QueryEngineStates), PlannedEngine(ClusterNodeDeploymentStep.StartEngine)));

        // Assert
        progress.Instances.Single().Timeline.Select(s => s.Step).Should().Equal(
            ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStep.QueryEngineStates, ClusterNodeDeploymentStep.StartEngine);
    }

    [Fact]
    public void Planned_step_the_instance_went_past_is_left_out()
    {
        // Arrange
        var progress = Start();
        progress.Apply(Planned(PlannedHost(ClusterNodeDeploymentStep.Prepare), PlannedHost(ClusterNodeDeploymentStep.UpdateEngineHost), PlannedHost(ClusterNodeDeploymentStep.StartEngineHost), PlannedEngine(ClusterNodeDeploymentStep.StartEngine)));
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Succeeded), s_receivedAt);

        // Act
        Receive(progress, HostStep(ClusterNodeDeploymentStep.StartEngineHost, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);

        // Assert
        progress.Instances.Single().Timeline.Select(s => (s.Step, s.State)).Should().Equal(
            (ClusterNodeDeploymentStep.Prepare, InstanceStepState.Succeeded),
            (ClusterNodeDeploymentStep.StartEngineHost, InstanceStepState.Running),
            (ClusterNodeDeploymentStep.StartEngine, InstanceStepState.Pending));
    }

    [Fact]
    public void Timeline_of_an_updated_instance_holds_only_its_steps()
    {
        // Arrange
        var progress = Start();
        progress.Apply(Planned(PlannedHost(ClusterNodeDeploymentStep.Prepare), PlannedEngine(ClusterNodeDeploymentStep.StartEngine)));
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Succeeded), s_receivedAt);

        // Act
        progress.Apply(InstanceChange(ClusterUpdateChange.Update));

        // Assert
        progress.Instances.Single().Timeline.Should().ContainSingle().Which.Step.Should().Be(ClusterNodeDeploymentStep.Prepare);
    }

    [Fact]
    public void Rollback_shows_the_pending_steps_of_its_own_plan()
    {
        // Arrange
        var progress = FailedUpdateThatRollsBack();

        // Act
        progress.Apply(Planned(PlannedHost(ClusterNodeDeploymentStep.Prepare), PlannedHost(ClusterNodeDeploymentStep.StopEngineHost), PlannedHost(ClusterNodeDeploymentStep.TearDownEngineHost)));

        // Assert
        progress.Instances.Single().Timeline.Select(s => (s.Step, s.State, s.IsRollback)).Should().Equal(
            (ClusterNodeDeploymentStep.DeployEngine, InstanceStepState.Failed, false),
            (ClusterNodeDeploymentStep.Prepare, InstanceStepState.Succeeded, true),
            (ClusterNodeDeploymentStep.StopEngineHost, InstanceStepState.Running, true),
            (ClusterNodeDeploymentStep.TearDownEngineHost, InstanceStepState.Pending, true));
    }

    [Fact]
    public void Planned_step_without_a_name_takes_it_from_a_known_version()
    {
        // Arrange
        var progress = Start();
        progress.KnowNamesOf(new Cluster
        {
            NodeGroups = [new ClusterNodeGroup { Nodes = [Node("edge-1", new ClusterApplication
            {
                Id = s_instanceId,
                Name = "CoreOS",
                Type = ClusterApplicationType.CoreOsMaster,
                EngineHosts = [new EngineHost { Id = s_engineHostId, Name = "Packaging", Engines = [new Engine { Id = s_engineId, Name = "Filling" }] }],
            })] }],
        });

        // Act
        progress.Apply(Planned(PlannedEngine(ClusterNodeDeploymentStep.TearDownEngine) with { EngineName = null }));

        // Assert
        progress.Instances.Single().Timeline.Single().Target.Should().Be("Filling");
    }

    [Fact]
    public void Matches_the_steps_of_its_deployment()
        => Start().Matches(s_clusterId, s_correlationId).Should().BeTrue();

    [Fact]
    public void Does_not_match_the_steps_of_another_cluster()
        => Start().Matches(Guid.NewGuid(), s_correlationId).Should().BeFalse();

    [Fact]
    public void Does_not_match_the_steps_of_another_deployment()
        => Start().Matches(s_clusterId, Guid.NewGuid()).Should().BeFalse();

    [Fact]
    public void Resumed_deployment_takes_the_steps_of_its_cluster()
    {
        // Arrange
        var target = new Cluster
        {
            NodeGroups = [new ClusterNodeGroup { Nodes = [Node("edge-1", new ClusterApplication { Id = s_instanceId, Name = "Master", Type = ClusterApplicationType.CoreOsMaster })] }],
        };
        var progress = DeploymentProgressModel.Resume(
            new DeploymentState(s_clusterId, "UpdatingDeployments", new Version(0, 1, 1), s_targetVersion) { CorrelationId = s_correlationId }, target);

        // Act
        Receive(progress, HostStep(ClusterNodeDeploymentStep.StartEngineHost, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);

        // Assert
        progress.Matches(s_clusterId, s_correlationId).Should().BeTrue();
        progress.Instances.Single().CurrentStep.Should().NotBeNull();
    }

    private static DeploymentProgressModel FailedUpdateThatRollsBack()
    {
        var progress = Start();
        progress.Apply(Transition("UpdatingDeployments"));
        Receive(progress, EngineStep(ClusterNodeDeploymentStep.DeployEngine, ClusterNodeDeploymentStepStatus.Failed, s_updateError), s_receivedAt);
        progress.Apply(InstanceChange(ClusterUpdateChange.Update, s_updateError));
        progress.Apply(Transition("RollingBackDeployments"));
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);
        Receive(progress, HostStep(ClusterNodeDeploymentStep.Prepare, ClusterNodeDeploymentStepStatus.Succeeded), s_receivedAt);
        Receive(progress, HostStep(ClusterNodeDeploymentStep.StopEngineHost, ClusterNodeDeploymentStepStatus.Started), s_receivedAt);
        return progress;
    }

    private static DeploymentProgressModel Start()
        => DeploymentProgressModel.Start(
            new ClusterUpdateStarted(s_correlationId, s_clusterId, s_targetVersion)
            {
                NodeApplications = new() { [s_nodeId] = s_instanceId },
                Nodes = new() { [s_nodeId] = "edge-1" },
                Applications = new() { [s_instanceId] = "CoreOS" },
            },
            DateTimeOffset.UnixEpoch);

    private static ClusterNode Node(string name, params ClusterApplication[] applications)
        => new() { Id = Guid.NewGuid(), Name = name, Applications = [.. applications] };

    private static ClusterUpdateChanged Transition(string sagaState)
        => new(s_correlationId, s_clusterId, s_targetVersion, sagaState) { Change = ClusterUpdateChange.Transition };

    private static ClusterUpdateChanged InstanceChange(ClusterUpdateChange change, ErrorInfo? error = null)
        => new(s_correlationId, s_clusterId, s_targetVersion, "UpdatingDeployments") { Change = change, ApplicationId = s_instanceId, Error = error };

    private static ClusterNodeDeploymentStepChanged HostStep(ClusterNodeDeploymentStep step, ClusterNodeDeploymentStepStatus status, ErrorInfo? error = null)
        => new(s_correlationId, s_clusterId, s_instanceId, step, status)
        {
            EngineHostId = s_engineHostId,
            EngineHostName = "Packaging",
            OccurredAt = DateTimeOffset.UnixEpoch,
            Error = error,
        };

    // On an instance whose clock agrees with the client's, so the steps take as long as their events lie apart.
    private static void Receive(DeploymentProgressModel progress, ClusterNodeDeploymentStepChanged step, DateTimeOffset receivedAt)
        => progress.Apply(step with { OccurredAt = receivedAt }, receivedAt);

    private static ClusterNodeDeploymentStepChanged EngineStep(ClusterNodeDeploymentStep step, ClusterNodeDeploymentStepStatus status, ErrorInfo? error = null)
        => HostStep(step, status, error) with { EngineId = s_engineId, EngineName = "Line 1" };

    private static ClusterNodeDeploymentPlanned Planned(params ClusterNodeDeploymentPlannedStep[] steps)
        => new(s_correlationId, s_clusterId, s_instanceId, steps);

    private static ClusterNodeDeploymentPlannedStep PlannedHost(ClusterNodeDeploymentStep step)
        => new(step) { EngineHostId = s_engineHostId, EngineHostName = "Packaging" };

    private static ClusterNodeDeploymentPlannedStep PlannedEngine(ClusterNodeDeploymentStep step)
        => PlannedHost(step) with { EngineId = s_engineId, EngineName = "Line 1" };

    private static ClusterUpdateFailed Failed(string? sagaState)
        => new(s_correlationId, s_clusterId, s_targetVersion)
        {
            State = sagaState,
            Reason = "At least one application update failed",
            Duration = TimeSpan.FromSeconds(8),
            ErrorsByApplication = new() { [s_instanceId] = new ErrorInfo(-1, "Error while connecting host") },
        };
}
