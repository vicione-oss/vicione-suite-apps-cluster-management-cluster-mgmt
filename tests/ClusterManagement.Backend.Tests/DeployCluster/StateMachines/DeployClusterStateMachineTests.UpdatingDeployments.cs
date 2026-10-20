using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Public.Commands;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

public partial class DeployClusterStateMachineTests
{
    public class UpdatingDeployments : DeployClusterStateMachineTests
    {
        private async Task TransitionToUpdatingDeployments(ITestHarness harness, Version targetVersion, DeployClusterOptions? options = null, bool deletePreviousVersion = false)
        {
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, targetVersion, options, deletePreviousVersion);

            if (options != null)
            {
                await harness.PublishClusterChangedCreated(_correlationId, _clusterId, targetVersion);
            }

            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);
            await harness.AssertDeployClusterState(_correlationId, x => x.UpdatingDeployments);
        }

        [Fact]
        public async Task Transition_to_cleaning_cluster_nodes_when_all_deployments_are_updated_and_nodes_need_cleanup()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();
            await TransitionToUpdatingDeployments(harness, prepared.Item2.Version);

            // Act            
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId, prepared.Item2.Version);

            // Assert
            await harness.AssertDeployClusterState(_correlationId, x => x.CleaningClusterNodes);

            var dbContext = provider.GetRequiredService<IClusterDbContext>();
            dbContext.ActiveClusters.Single(k => k.Id == _clusterId).Version.Should().Be(prepared.Item2!.Version, "Active cluster should be already updated");
        }

        [Fact]
        public async Task Transition_to_cleaning_cluster_when_all_deployments_are_updated_and_previous_cluster_should_not_be_kept()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithTargetOnSameNode(provider);
            var target = prepared.Item2;

            var harness = provider.GetTestHarness();
            await TransitionToUpdatingDeployments(harness, target.Version, deletePreviousVersion: true);

            // Act            
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId, target.Version);

            // Assert
            Assert.True(await harness.Sent.Any<RemoveCluster>(k
                => k.Context.Message.ClusterId == _clusterId
                && k.Context.Message.Version == prepared.Item1.Version, TestContext.Current.CancellationToken));

            await harness.AssertDeployClusterState(_correlationId, x => x.CleaningCluster);

            var dbContext = provider.GetRequiredService<IClusterDbContext>();
            dbContext.ActiveClusters.Single(k => k.Id == _clusterId).Version.Should().Be(prepared.Item2.Version, "Active cluster should be already updated");
        }

        [Fact]
        public async Task Transition_to_rolling_back_deployments_when_at_least_one_update_failed()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();
            await TransitionToUpdatingDeployments(harness, prepared.Item2.Version);

            // Act            
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_correlationId, _clusterId, _applicationId, prepared.Item2.Version);

            // Assert
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackDeployments);
        }

        [Fact]
        public async Task Should_fail_if_active_cluster_json_cannot_be_loaded_for_rollback()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            _snapshotStore.LoadCompressedJson(_clusterId, prepared.Item1!.Version, Arg.Any<CancellationToken>())
                .Returns(Task.FromException<byte[]>(new InvalidOperationException("Snapshot not found")));
            var harness = provider.GetTestHarness();
            await TransitionToUpdatingDeployments(harness, prepared.Item2.Version);

            // Act
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_correlationId, _clusterId, _applicationId, prepared.Item2.Version);

            // Assert
            await harness.AssertClusterUpdateFailed(_correlationId, prepared.Item2.Version);
            var sagaHarness = harness.GetSagaStateMachineHarness<DeployClusterStateMachine, DeployClusterState>();
            Assert.Null(await sagaHarness.NotExists(_correlationId));
        }

        [Fact]
        public async Task Finalize_when_all_deployents_are_updated_and_nodes_dont_need_cleanup()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithTargetOnSameNode(provider);
            var target = prepared.Item2;

            var harness = provider.GetTestHarness();
            await TransitionToUpdatingDeployments(harness, target.Version);

            // Act
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId, target.Version);

            // Assert
            await harness.AssertClusterUpdateCompleted(_correlationId, target.Version);

            var dbContext = provider.GetRequiredService<IClusterDbContext>();
            dbContext.ActiveClusters.Single(k => k.Id == _clusterId).Version.Should().Be(prepared.Item2!.Version, "Active cluster should be already updated");
        }

        [Fact]
        public async Task Should_reject_deploy_cluster_if_saga_is_updating_deployments()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();
            await TransitionToUpdatingDeployments(harness, prepared.Item2.Version);

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, _deployAttemptVersion);

            // Assert
            await harness.AssertDeployClusterRejected(_correlationId, _deployAttemptVersion);
        }
    }
}
