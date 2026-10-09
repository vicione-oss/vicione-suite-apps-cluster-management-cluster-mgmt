using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Backend.Tests.Extensions;
using MassTransit.Testing;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

public partial class DeployClusterStateMachineTests
{
    public class AddingClusterNodes : DeployClusterStateMachineTests
    {
        [Fact]
        public async Task Transition_to_update_deployments_when_all_nodes_are_running()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);

            // Act            
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);

            // Assert
            await harness.AssertDeployClusterState(_correlationId, x => x.UpdatingDeployments);
        }

        [Fact]
        public async Task Should_fail_if_target_cluster_json_cannot_be_loaded_after_nodes_were_added()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            _snapshotStore.LoadCompressedJson(_clusterId, prepared.Item2.Version, Arg.Any<CancellationToken>())
                .Returns(Task.FromException<byte[]>(new InvalidOperationException("Snapshot not found")));
            var harness = provider.GetTestHarness();

            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);
            await harness.AssertDeployClusterState(_correlationId, x => x.AddingClusterNodes);

            // Act
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);

            // Assert
            await harness.AssertClusterUpdateFailed(_correlationId, prepared.Item2.Version);
            var sagaHarness = harness.GetSagaStateMachineHarness<DeployClusterStateMachine, DeployClusterState>();
            Assert.Null(await sagaHarness.NotExists(_correlationId));
        }

        [Fact]
        public async Task Should_reject_deploy_cluster_if_saga_is_adding_cluster_nodes()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);
            await harness.AssertDeployClusterState(_correlationId, x => x.AddingClusterNodes);

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, _deployAttemptVersion);

            // Assert
            await harness.AssertDeployClusterRejected(_correlationId, _deployAttemptVersion);
        }
    }
}
