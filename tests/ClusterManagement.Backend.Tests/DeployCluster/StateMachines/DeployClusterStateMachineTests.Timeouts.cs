using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Internal;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using ClusterManagement.Tests.Tools;
using MassTransit;
using MassTransit.Testing;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

public partial class DeployClusterStateMachineTests
{
    public class Timeouts : DeployClusterStateMachineTests
    {
        private static readonly TimeSpan s_shortTimeout = TimeSpan.FromMilliseconds(200);

        // Leaves time for the replies of the instances that answer
        private static readonly TimeSpan s_updateTimeout = TimeSpan.FromMilliseconds(500);

        private readonly Guid _secondNodeId = Guid.Parse("DDDDDDDD-1111-414A-90BE-6F4BA877A9CF");
        private readonly Guid _secondApplicationId = Guid.Parse("DDDDDDDD-2222-414A-90BE-6F4BA877A9CF");

        private static ClusterManagementOptions CreateOptions(Action<DeploySagaTimeoutOptions> configure)
        {
            var options = new ClusterManagementOptions();
            options.DeploySagaTimeouts.CheckInterval = TimeSpan.FromMilliseconds(50);
            configure(options.DeploySagaTimeouts);
            return options;
        }

        private (Cluster, Cluster) SetupSagaClusterWithTwoInstances(IServiceProvider services)
            => services.PrepareDeployClusterSaga(_snapshotStore,
                _ => CreateClusterWithTwoInstances(_activeVersion),
                _ => CreateClusterWithTwoInstances(_targetVersion));

        private Cluster CreateClusterWithTwoInstances(Version version)
        {
            var cluster = ClusterDbContextTools.CreateCluster(_clusterId, version);
            var group = AddNodeGroup(cluster, _nodeGroupId);

            AddClusterApplication(group, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsSlave);
            AddClusterApplication(group, _secondNodeId, _secondApplicationId, ClusterApplicationType.CoreOsSlave);

            return cluster;
        }

        private static async Task<Version> LastUpdateDeploymentsVersion(ITestHarness harness)
        {
            var sent = await harness.Published.SelectAsync<UpdateDeployments>(TestContext.Current.CancellationToken)
                .ToListAsync(TestContext.Current.CancellationToken);
            return sent[^1].Context.Message.ClusterVersion;
        }

        private async Task AssertTimeoutFailure(ITestHarness harness, string state)
            => Assert.True(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.CorrelationId == _correlationId
                && k.Context.Message.Reason != null
                && k.Context.Message.Reason.Contains($"in state {state}", StringComparison.Ordinal), TestContext.Current.CancellationToken),
                $"Deploy cluster failed event with timeout in state {state} not published");

        private async Task AssertFinalized(ITestHarness harness)
        {
            var sagaHarness = harness.GetSagaStateMachineHarness<DeployClusterStateMachine, DeployClusterState>();
            Assert.Null(await sagaHarness.NotExists(_correlationId));
        }

        [Fact]
        public async Task Committing_cluster_timeout_fails_and_finalizes()
        {
            // Arrange
            await using var provider = SetupServiceProvider(CreateOptions(t => t.CommittingCluster = s_shortTimeout));

            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            SetupSagaClusterActiveVersionOnly(provider);

            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act: cluster change is never committed
            await harness.DeployCluster(_correlationId, _clusterId, null, options);

            // Assert
            await AssertTimeoutFailure(harness, nameof(DeployClusterStateMachine.CommittingCluster));
            await AssertFinalized(harness);
        }

        [Fact]
        public async Task Adding_cluster_nodes_timeout_cleans_created_nodes_fails_and_finalizes()
        {
            // Arrange
            await using var provider = SetupServiceProvider(CreateOptions(t => t.AddingClusterNodes = s_shortTimeout));

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act: node is added but its application never starts
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);

            // Assert
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackClusterNodes);
            Assert.True(await harness.Sent.Any<CleanClusterNodesCommand>(k => k.Context.Message.CorrelationId == _correlationId
                && k.Context.Message.NodesToClean.Contains(_nodeIdToAdd), TestContext.Current.CancellationToken),
                "Created nodes were not cleaned up");

            await harness.PublishClusterNodeCleaned(_correlationId, _clusterId, _nodeIdToAdd);

            await AssertTimeoutFailure(harness, nameof(DeployClusterStateMachine.AddingClusterNodes));
            await AssertFinalized(harness);
        }

        [Fact]
        public async Task Rolling_back_cluster_nodes_timeout_fails_and_finalizes()
        {
            // Arrange
            await using var provider = SetupServiceProvider(CreateOptions(t =>
            {
                t.AddingClusterNodes = s_shortTimeout;
                t.RollingBackClusterNodes = s_shortTimeout;
            }));

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act: node is added but neither its application starts nor is the node cleaned up
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);

            // Assert
            await AssertTimeoutFailure(harness, nameof(DeployClusterStateMachine.AddingClusterNodes));
            await AssertFinalized(harness);
        }

        [Fact]
        public async Task Updating_deployments_timeout_without_any_answer_sends_the_rollback_and_fails()
        {
            // Arrange
            await using var provider = SetupServiceProvider(CreateOptions(t => t.UpdatingDeployments = s_shortTimeout));

            SetupSagaClusterWithTargetOnSameNode(provider);
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act: the application never answers
            await harness.DeployCluster(_correlationId, _clusterId, _targetVersion);

            // Assert: the offline instance gets the rollback for when it comes back, but nothing waits for it
            await AssertTimeoutFailure(harness, nameof(DeployClusterStateMachine.UpdatingDeployments));
            await AssertFinalized(harness);
            Assert.Equal(_activeVersion, await LastUpdateDeploymentsVersion(harness));
        }

        [Fact]
        public async Task Late_replies_after_the_deployment_ended_are_dropped()
        {
            // Arrange
            await using var provider = SetupServiceProvider(CreateOptions(t => t.UpdatingDeployments = s_shortTimeout));

            SetupSagaClusterWithTargetOnSameNode(provider);
            var harness = provider.GetTestHarness();
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, _targetVersion);
            await AssertTimeoutFailure(harness, nameof(DeployClusterStateMachine.UpdatingDeployments));
            await AssertFinalized(harness);

            // Act: the offline instance comes back and answers the update and the rollback
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId, _targetVersion);
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_correlationId, _clusterId, _applicationId, _activeVersion);

            // Assert: events that can't start a deployment are dropped if their deployment is gone
            Assert.True(await harness.Consumed.Any<ClusterNodeDeploymentsUpdateFailedEvent>(TestContext.Current.CancellationToken),
                "Late reply was not consumed");
            Assert.False(await harness.Published.Any<Fault<ClusterNodeDeploymentsUpdatedEvent>>(TestContext.Current.CancellationToken));
            Assert.False(await harness.Published.Any<Fault<ClusterNodeDeploymentsUpdateFailedEvent>>(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Updating_deployments_timeout_rolls_back_only_the_instances_that_answered()
        {
            // Arrange
            await using var provider = SetupServiceProvider(CreateOptions(t => t.UpdatingDeployments = s_updateTimeout));

            SetupSagaClusterWithTwoInstances(provider);
            var harness = provider.GetTestHarness();
            var sagaHarness = harness.GetSagaStateMachineHarness<DeployClusterStateMachine, DeployClusterState>();
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, _targetVersion);
            await harness.AssertDeployClusterState(_correlationId, x => x.UpdatingDeployments);

            // Act: the second instance is offline and answers the update only after the timeout started the rollback
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId, _targetVersion);
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackDeployments);
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _secondApplicationId, _targetVersion);

            // Assert: the late reply is no rollback reply, and the rollback only waits for the first instance
            Assert.True(await sagaHarness.Consumed.Any<ClusterNodeDeploymentsUpdatedEvent>(k => k.Context.Message.ClusterApplicationId == _secondApplicationId,
                TestContext.Current.CancellationToken), "Late reply was not consumed");
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackDeployments);

            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId, _activeVersion);

            await AssertTimeoutFailure(harness, nameof(DeployClusterStateMachine.UpdatingDeployments));
            await AssertFinalized(harness);
        }

        [Fact]
        public async Task Rolling_back_deployments_timeout_fails_and_finalizes_keeping_first_reason()
        {
            // Arrange
            await using var provider = SetupServiceProvider(CreateOptions(t =>
            {
                t.UpdatingDeployments = s_updateTimeout;
                t.RollingBackDeployments = s_shortTimeout;
            }));

            SetupSagaClusterWithTwoInstances(provider);
            var harness = provider.GetTestHarness();
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, _targetVersion);
            await harness.AssertDeployClusterState(_correlationId, x => x.UpdatingDeployments);

            // Act: the first instance answers the update but not the rollback, the second one is offline
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId, _targetVersion);
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackDeployments);

            // Assert
            await AssertTimeoutFailure(harness, nameof(DeployClusterStateMachine.UpdatingDeployments));
            await AssertFinalized(harness);
        }

        [Fact]
        public async Task Rolling_back_commit_timeout_fails_and_finalizes()
        {
            // Arrange
            await using var provider = SetupServiceProvider(CreateOptions(t => t.RollingBackCommit = s_shortTimeout));

            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            SetupSagaClusterActiveVersionOnly(provider);

            var harness = provider.GetTestHarness();
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, null, options);
            await harness.PublishClusterChangedCreated(_correlationId, _clusterId, commitVersion);
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_correlationId, _clusterId, _applicationId, commitVersion);
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackDeployments);

            // Act: rollback succeeds but removing the committed version is never confirmed
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId, _activeVersion);

            // Assert
            await AssertTimeoutFailure(harness, nameof(DeployClusterStateMachine.RollingBackCommit));
            await AssertFinalized(harness);
        }

        [Fact]
        public async Task Cleaning_cluster_nodes_timeout_completes_update()
        {
            // Arrange
            await using var provider = SetupServiceProvider(CreateOptions(t => t.CleaningClusterNodes = s_shortTimeout));

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);

            // Act: deployment succeeds but the obsolete node is never cleaned up
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId, prepared.Item2.Version);

            // Assert
            await harness.AssertClusterUpdateCompleted(_correlationId, prepared.Item2.Version);
            await AssertFinalized(harness);
        }

        [Fact]
        public async Task Timeout_of_a_left_state_is_ignored()
        {
            // Arrange
            await using var provider = SetupServiceProvider(CreateOptions(t => t.UpdatingDeployments = s_shortTimeout));

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);

            // Act: leave UpdatingDeployments before its timeout is due
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId, prepared.Item2.Version);
            await Task.Delay(s_shortTimeout * 3, TestContext.Current.CancellationToken);

            // Assert
            await harness.AssertDeployClusterState(_correlationId, x => x.CleaningClusterNodes);
            Assert.False(await harness.Published.Any<ClusterUpdateFailed>(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Timeout_of_a_state_that_is_not_overdue_is_ignored()
        {
            // Arrange
            await using var provider = SetupServiceProvider(CreateOptions(_ => { }));

            SetupSagaClusterWithTargetOnSameNode(provider);
            var harness = provider.GetTestHarness();
            var sagaHarness = harness.GetSagaStateMachineHarness<DeployClusterStateMachine, DeployClusterState>();
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, _targetVersion);
            await harness.AssertDeployClusterState(_correlationId, x => x.UpdatingDeployments);

            // Act: the watchdog found the saga overdue in a state it left meanwhile
            await harness.Bus.Publish(new DeployClusterStateTimeout(_correlationId), TestContext.Current.CancellationToken);

            // Assert
            Assert.True(await sagaHarness.Consumed.Any<DeployClusterStateTimeout>(TestContext.Current.CancellationToken), "Timeout was not consumed");
            await harness.AssertDeployClusterState(_correlationId, x => x.UpdatingDeployments);
            Assert.False(await harness.Published.Any<ClusterUpdateFailed>(TestContext.Current.CancellationToken));
        }
    }
}
