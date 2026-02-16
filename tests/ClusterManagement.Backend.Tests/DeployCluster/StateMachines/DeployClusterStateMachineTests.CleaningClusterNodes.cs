using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Public.Commands;
using MassTransit.Testing;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

public partial class DeployClusterStateMachineTests
{
    public class CleaningClusterNodes : DeployClusterStateMachineTests
    {
        [Fact]
        public async Task Finalize_when_all_nodes_are_cleaned_up()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId);
            await harness.AssertDeployClusterState(_correlationId, x => x.CleaningClusterNodes);

            // Act
            await harness.PublishClusterNodeCleaned(_correlationId, _clusterId, _nodeIdToClean);

            // Assert
            await harness.AssertClusterUpdateCompleted(_correlationId, prepared.Item2.Version);
        }

        [Fact]
        public async Task Should_reject_deploy_cluster_if_saga_is_cleaning_cluster_nodes()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId);
            await harness.AssertDeployClusterState(_correlationId, x => x.CleaningClusterNodes);

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, _deployAttemptVersion);

            // Assert
            await harness.AssertDeployClusterRejected(_correlationId, _deployAttemptVersion);
        }

        [Fact]
        public async Task Transition_to_cleaning_cluster_if_previous_version_should_not_be_kept()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version, deletePreviousVersion: true);
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId);

            // Act
            await harness.PublishClusterNodeCleaned(_correlationId, _clusterId, _nodeIdToClean);

            // Assert
            await harness.AssertDeployClusterState(_correlationId, x => x.CleaningCluster);

            Assert.True(await harness.Sent.Any<RemoveCluster>(k
                => k.Context.Message.ClusterId == _clusterId
                && k.Context.Message.Version == prepared.Item1.Version, TestContext.Current.CancellationToken));
        }
    }
}
