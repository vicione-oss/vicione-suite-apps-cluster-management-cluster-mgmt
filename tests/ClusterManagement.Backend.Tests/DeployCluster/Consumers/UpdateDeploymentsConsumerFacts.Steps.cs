using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Public.ErrorHandling;
using ClusterManagement.Public.Events;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using Sdk.Instance;
using Status = ClusterManagement.Public.Enums.ClusterNodeDeploymentStepStatus;
using Step = ClusterManagement.Public.Enums.ClusterNodeDeploymentStep;

namespace ClusterManagement.Backend.Tests.DeployCluster.Consumers;

public partial class UpdateDeploymentsConsumerFacts
{
    public class Steps : UpdateDeploymentsConsumerFacts
    {
        private readonly Guid _engineHost1 = new("e1000000-0000-0000-0000-000000000000");
        private readonly Guid _engine1 = new("00000000-e100-0000-0000-000000000000");
        private readonly Guid _engine2 = new("00000000-e200-0000-0000-000000000000");
        private readonly Guid _functionBlock1 = new("00000000-0000-f100-0000-000000000000");

        [Fact]
        public async Task Publishes_every_step_of_a_new_engineHost_as_started_and_succeeded()
        {
            Given_configured_suite();
            Given_user_added_engineHost(_engineHost1);
            Given_user_added_engine(_engine1, _engineHost1);
            Given_user_added_functionBlock(_engine1, _functionBlock1);

            await When_updating_deployments();

            Then_update_succeeded();
            Then_steps_were_published(
                (Step.Prepare, Status.Started, null, null),
                (Step.Prepare, Status.Succeeded, null, null),
                (Step.Plan, Status.Started, null, null),
                (Step.Plan, Status.Succeeded, null, null),
                (Step.DeployEngineHost, Status.Started, _engineHost1, null),
                (Step.DeployEngineHost, Status.Succeeded, _engineHost1, null),
                (Step.StartEngineHost, Status.Started, _engineHost1, null),
                (Step.StartEngineHost, Status.Succeeded, _engineHost1, null),
                (Step.QueryEngineStates, Status.Started, _engineHost1, null),
                (Step.QueryEngineStates, Status.Succeeded, _engineHost1, null),
                (Step.DeployEngine, Status.Started, _engineHost1, _engine1),
                (Step.DeployEngine, Status.Succeeded, _engineHost1, _engine1),
                (Step.StartEngine, Status.Started, _engineHost1, _engine1),
                (Step.StartEngine, Status.Succeeded, _engineHost1, _engine1));
        }

        [Fact]
        public async Task Names_the_engineHost_and_engine_of_a_step()
        {
            Given_configured_suite();
            Given_user_added_engineHost(_engineHost1);
            Given_user_added_engine(_engine1, _engineHost1);
            Given_user_added_functionBlock(_engine1, _functionBlock1);

            await When_updating_deployments();

            var startEngine = _steps.First(s => s.Step == Step.StartEngine);
            startEngine.EngineHostName.Should().Be(_engineHost1.ToString());
            startEngine.EngineName.Should().Be(_engine1.ToString());
            startEngine.CorrelationId.Should().Be(_correlationId);
            startEngine.ClusterId.Should().Be(_clusterBuilder.Cluster.Id);
            startEngine.ClusterApplicationId.Should().Be(_instanceInformation.Id);
        }

        [Fact]
        public async Task Stamps_every_step_with_the_time_of_the_instance()
        {
            var now = new DateTimeOffset(2026, 10, 9, 14, 2, 11, TimeSpan.Zero);
            Given_configured_suite();
            Given_user_added_engineHost(_engineHost1);
            Given_user_added_engine(_engine1, _engineHost1);
            Given_user_added_functionBlock(_engine1, _functionBlock1);
            _clock.SetUtcNow(now);

            await When_updating_deployments();

            _steps.Should().NotBeEmpty().And.OnlyContain(s => s.OccurredAt == now);
        }

        [Fact]
        public async Task Publishes_the_failed_engineHost_step_with_the_error_the_instance_reports()
        {
            Given_configured_suite();
            Given_user_added_engineHost(_engineHost1);
            Given_user_added_engine(_engine1, _engineHost1);
            Given_user_added_functionBlock(_engine1, _functionBlock1);
            Given_start_engineHost_will_fail(_engineHost1);

            await When_updating_deployments();

            Then_update_failed(ClusterErrors.CouldNotStartEngineHost);
            Then_last_step_failed_with_the_reported_error(Step.StartEngineHost, _engineHost1, null);
        }

        [Fact]
        public async Task Publishes_the_failed_engine_step_with_the_error_the_instance_reports()
        {
            Given_configured_suite();
            Given_user_added_engineHost(_engineHost1);
            Given_user_added_engine(_engine1, _engineHost1);
            Given_user_added_functionBlock(_engine1, _functionBlock1);
            Given_deploy_engine_will_fail(_engineHost1, _engine1);

            await When_updating_deployments();

            Then_update_failed(ClusterErrors.CouldNotDeployEngine);
            Then_last_step_failed_with_the_reported_error(Step.DeployEngine, _engineHost1, _engine1);
        }

        [Fact]
        public async Task Publishes_no_step_for_a_stopped_engine_and_no_engine_for_an_unexpected_deployment()
        {
            Given_configured_suite();
            Given_user_added_engineHost(_engineHost1);
            Given_user_added_engine(_engine1, _engineHost1);
            Given_user_disabled_engine(_engine1);
            Given_user_added_functionBlock(_engine1, _functionBlock1);
            Given_engineHost_is_known(_engineHost1);
            Given_engineHost_is_deployed(_engineHost1);
            Given_engineHost_is_started(_engineHost1);
            Given_engine_is_known(_engineHost1, _engine1, true);
            Given_engine_is_deployed(_engineHost1, _engine1);
            Given_engine_has_functionBlock(_engine1, _functionBlock1);
            Given_engine_is_deployed(_engineHost1, _engine2);

            await When_updating_deployments();

            Then_update_succeeded();
            Then_steps_were_published(
                (Step.Prepare, Status.Started, null, null),
                (Step.Prepare, Status.Succeeded, null, null),
                (Step.Plan, Status.Started, null, null),
                (Step.Plan, Status.Succeeded, null, null),
                (Step.CompareEngineHostSettings, Status.Started, _engineHost1, null),
                (Step.CompareEngineHostSettings, Status.Succeeded, _engineHost1, null),
                (Step.QueryEngineStates, Status.Started, _engineHost1, null),
                (Step.QueryEngineStates, Status.Succeeded, _engineHost1, null),
                (Step.TearDownEngine, Status.Started, _engineHost1, null),
                (Step.TearDownEngine, Status.Succeeded, _engineHost1, null));
        }

        [Fact]
        public async Task Publishes_the_steps_that_remove_an_engineHost_and_the_retained_messages_of_its_engine()
        {
            Given_cluster_node_group();
            Given_cluster_node();
            Given_engineHost_is_known(_engineHost1);
            Given_engineHost_is_deployed(_engineHost1);
            Given_engineHost_is_started(_engineHost1);
            Given_engine_is_known(_engineHost1, _engine1);
            Given_engine_is_deployed(_engineHost1, _engine1);
            Given_engine_is_started(_engineHost1, _engine1);
            Given_engine_has_functionBlock(_engine1, _functionBlock1);

            await When_updating_deployments();

            Then_update_succeeded();
            Then_steps_were_published(
                (Step.Prepare, Status.Started, null, null),
                (Step.Prepare, Status.Succeeded, null, null),
                (Step.Plan, Status.Started, null, null),
                (Step.Plan, Status.Succeeded, null, null),
                (Step.QueryEngineStates, Status.Started, _engineHost1, null),
                (Step.QueryEngineStates, Status.Succeeded, _engineHost1, null),
                (Step.StopEngine, Status.Started, _engineHost1, _engine1),
                (Step.StopEngine, Status.Succeeded, _engineHost1, _engine1),
                (Step.TearDownEngine, Status.Started, _engineHost1, _engine1),
                (Step.TearDownEngine, Status.Succeeded, _engineHost1, _engine1),
                (Step.StopEngineHost, Status.Started, _engineHost1, null),
                (Step.StopEngineHost, Status.Succeeded, _engineHost1, null),
                (Step.TearDownEngineHost, Status.Started, _engineHost1, null),
                (Step.TearDownEngineHost, Status.Succeeded, _engineHost1, null),
                (Step.DeleteRetainedMessages, Status.Started, null, null),
                (Step.DeleteRetainedMessages, Status.Succeeded, null, null));
        }

        [Fact]
        public async Task Publishes_the_restart_of_an_engineHost_whose_settings_changed()
        {
            Given_configured_suite();
            Given_user_added_engineHost(_engineHost1);
            Given_user_added_engine(_engine1, _engineHost1);
            Given_user_added_functionBlock(_engine1, _functionBlock1);
            Given_engineHost_is_known(_engineHost1);
            Given_engineHost_is_deployed(_engineHost1);
            Given_engineHost_is_started(_engineHost1);
            Given_engineHost_has_different_appsettings(_engineHost1);
            Given_engine_is_known(_engineHost1, _engine1, true);
            Given_engine_is_deployed(_engineHost1, _engine1);
            Given_engine_is_started(_engineHost1, _engine1);
            Given_engine_has_functionBlock(_engine1, _functionBlock1);

            await When_updating_deployments();

            Then_update_succeeded();
            Then_steps_were_published(
                (Step.Prepare, Status.Started, null, null),
                (Step.Prepare, Status.Succeeded, null, null),
                (Step.Plan, Status.Started, null, null),
                (Step.Plan, Status.Succeeded, null, null),
                (Step.CompareEngineHostSettings, Status.Started, _engineHost1, null),
                (Step.CompareEngineHostSettings, Status.Succeeded, _engineHost1, null),
                (Step.StopEngineHost, Status.Started, _engineHost1, null),
                (Step.StopEngineHost, Status.Succeeded, _engineHost1, null),
                (Step.UpdateEngineHost, Status.Started, _engineHost1, null),
                (Step.UpdateEngineHost, Status.Succeeded, _engineHost1, null),
                (Step.StartEngineHost, Status.Started, _engineHost1, null),
                (Step.StartEngineHost, Status.Succeeded, _engineHost1, null),
                (Step.QueryEngineStates, Status.Started, _engineHost1, null),
                (Step.QueryEngineStates, Status.Succeeded, _engineHost1, null),
                (Step.StartEngine, Status.Started, _engineHost1, _engine1),
                (Step.StartEngine, Status.Succeeded, _engineHost1, _engine1));
        }

        [Fact]
        public async Task Publishes_the_step_a_cancelled_deployment_stopped_at_with_the_error_the_instance_reports()
        {
            Given_configured_suite();
            Given_user_added_engineHost(_engineHost1);
            Given_user_added_engine(_engine1, _engineHost1);
            Given_user_added_functionBlock(_engine1, _functionBlock1);
            Given_deploying_the_engineHost_cancels_the_deployment(_engineHost1);

            await When_updating_deployments();

            Then_update_failed(ClusterErrors.DeploymentCancelled);
            Then_last_step_failed_with_the_reported_error(Step.DeployEngineHost, _engineHost1, null);
        }

        [Fact]
        public async Task Publishes_a_start_the_engine_host_refuses_as_refused()
        {
            Given_configured_suite();
            Given_user_added_engineHost(_engineHost1);
            Given_user_added_engine(_engine1, _engineHost1);
            Given_user_added_functionBlock(_engine1, _functionBlock1);
            _hostCommunication.StartEngine(Arg.Is(_engineHost1), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

            await When_updating_deployments();

            Then_update_succeeded();
            _steps.Select(s => (s.Step, s.Status)).TakeLast(2).Should().Equal(
                (Step.StartEngine, Status.Started),
                (Step.StartEngine, Status.Refused));
        }

        [Fact]
        public async Task Publishes_no_failed_step_for_a_failure_after_the_steps()
        {
            Given_configured_suite();
            Given_user_added_engineHost(_engineHost1);
            Given_user_added_engine(_engine1, _engineHost1);
            Given_user_added_functionBlock(_engine1, _functionBlock1);
            _instanceInformation.Type.Returns(InstanceType.Slave);
            _clusterDbContext.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("Database unavailable"));

            await When_updating_deployments();

            Then_update_failed(-1);
            _steps.Should().NotContain(s => s.Status == Status.Failed);
            _steps.Last().Should().Match<ClusterNodeDeploymentStepChanged>(s => s.Step == Step.StartEngine && s.Status == Status.Succeeded);
        }

        [Fact]
        public async Task Deploys_without_waiting_again_for_a_broker_that_did_not_confirm_a_step()
        {
            Given_configured_suite();
            Given_user_added_engineHost(_engineHost1);
            Given_user_added_engine(_engine1, _engineHost1);
            Given_user_added_functionBlock(_engine1, _functionBlock1);
            _bus.Publish(Arg.Any<ClusterNodeDeploymentStepChanged>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>()));

            await When_updating_deployments_while_every_publish_times_out();

            Then_update_succeeded_with_step_warnings();
            _steps.Should().ContainSingle("no step is published after the broker failed to confirm one");
            await Then_engine_gets_started(_engineHost1, _engine1);
        }

        [Fact]
        public async Task Deploys_although_no_step_can_be_published()
        {
            Given_configured_suite();
            Given_user_added_engineHost(_engineHost1);
            Given_user_added_engine(_engine1, _engineHost1);
            Given_user_added_functionBlock(_engine1, _functionBlock1);
            Given_publishing_steps_will_fail();

            await When_updating_deployments();

            Then_update_succeeded_with_step_warnings();
            await Then_engine_gets_started(_engineHost1, _engine1);
        }

        private void Given_deploying_the_engineHost_cancels_the_deployment(Guid id)
            => _poolCommunication.DeployHost(Arg.Is(id), Arg.Any<CancellationToken>()).Returns(_ =>
            {
                _deploymentCancellation.Cancel();
                return Task.FromException(new OperationCanceledException(_deploymentCancellation.Token));
            });

        // Lets the publish timeout expire as often as the deployment waits for it.
        private async Task When_updating_deployments_while_every_publish_times_out()
        {
            var updating = When_updating_deployments();
            for (var round = 0; !updating.IsCompleted; round++)
            {
                round.Should().BeLessThan(1000, "the deployment ends");
                _clock.Advance(TimeSpan.FromSeconds(5));
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }

            await updating;
        }

        private void Given_publishing_steps_will_fail()
            => _bus.Publish(Arg.Any<ClusterNodeDeploymentStepChanged>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("Broker unavailable"));

        private void Then_steps_were_published(params (Step Step, Status Status, Guid? EngineHostId, Guid? EngineId)[] expected)
            => _steps.Select(s => (s.Step, s.Status, s.EngineHostId, s.EngineId))
                .Should().Equal(expected);

        private void Then_last_step_failed_with_the_reported_error(Step step, Guid engineHostId, Guid? engineId)
        {
            var reported = _result.Should().BeOfType<ClusterNodeDeploymentsUpdateFailedEvent>().Subject.ErrorInfo;
            var failed = _steps.Last();
            (failed.Step, failed.Status, failed.EngineHostId, failed.EngineId).Should().Be((step, Status.Failed, engineHostId, engineId));
            failed.Error.Should().BeEquivalentTo(reported);
        }

        private void Then_update_succeeded_with_step_warnings()
        {
            _result.Should().BeOfType<ClusterNodeDeploymentsUpdatedEvent>();
            _logger.LogEntries.Should().NotContain(e => e.LogLevel >= LogLevel.Error);
            _logger.LogEntries.Should().Contain(e => e.LogLevel == LogLevel.Warning && e.Message.Contains("Could not publish step"));
        }
    }
}
