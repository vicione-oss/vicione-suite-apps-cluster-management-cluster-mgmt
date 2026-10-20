using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Public.Events;
using MassTransit.Testing;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

public partial class DeployClusterStateMachineTests
{
    public class CleaningCluster : DeployClusterStateMachineTests
    {
        private async Task TransitionToCleaningCluster(ITestHarness harness, Version targetVersion)
        {
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, targetVersion, deletePreviousVersion: true);
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId, targetVersion);
            await harness.AssertDeployClusterState(_correlationId, x => x.CleaningCluster);
        }

        [Fact]
        public async Task Should_consume_change_event_if_previous_cluster_was_deleted_and_finalize()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithTargetOnSameNode(provider);
            var harness = provider.GetTestHarness();

            await TransitionToCleaningCluster(harness, prepared.Item2.Version);

            // Act
            await harness.PublishClusterChangedDeleted(_correlationId, _clusterId, prepared.Item1.Version);

            // Assert
            await harness.Consumed.Any<ClusterChanged>(k => k.Context.Message.CorrelationId == _correlationId
                && k.Context.Message.ClusterId == _clusterId
                && k.Context.Message.Action == CrudAction.Deleted
                && k.Context.Message.Version == prepared.Item1.Version, TestContext.Current.CancellationToken);

            await harness.AssertClusterUpdateCompleted(_correlationId, prepared.Item2.Version);
        }

        [Fact]
        public async Task Should_ignore_unrelated_change_events()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithTargetOnSameNode(provider);
            var harness = provider.GetTestHarness();

            await TransitionToCleaningCluster(harness, prepared.Item2.Version);

            // Act
            await harness.PublishClusterChangedDeleted(Guid.NewGuid(), Guid.NewGuid(), new Version(2, 2, 2));

            // Assert
            Assert.False(await harness.Consumed.Any<ClusterUpdateFailed>(k => k.Context.Message.Version == prepared.Item2.Version, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_stay_cleaning_cluster_on_events_of_another_version()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithTargetOnSameNode(provider);
            var harness = provider.GetTestHarness();

            await TransitionToCleaningCluster(harness, prepared.Item2.Version);

            // Act
            await harness.PublishClusterChangedDeleted(_correlationId, _clusterId, new Version(2, 2, 2));
            await harness.PublishClusterErrorDeleteFailed(_correlationId, _clusterId, new Version(2, 2, 2));

            // Assert
            Assert.False(await harness.Published.Any<ClusterUpdateCompleted>(k => k.Context.CorrelationId == _correlationId, TestContext.Current.CancellationToken));
            await harness.AssertDeployClusterState(_correlationId, x => x.CleaningCluster);
        }

        [Fact]
        public async Task Should_consume_error_event_if_previous_cluster_delete_failed_and_finalize()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithTargetOnSameNode(provider);
            var harness = provider.GetTestHarness();

            await TransitionToCleaningCluster(harness, prepared.Item2.Version);

            // Act
            await harness.PublishClusterErrorDeleteFailed(_correlationId, _clusterId, prepared.Item1.Version);

            // Assert
            await harness.Consumed.Any<ClusterError>(k => k.Context.Message.ClusterId == _clusterId, TestContext.Current.CancellationToken);

            await harness.AssertClusterUpdateCompleted(_correlationId, prepared.Item2.Version);
        }

        [Fact]
        public async Task Should_reject_deploy_cluster_if_saga_is_cleaning_cluster()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithTargetOnSameNode(provider);
            var harness = provider.GetTestHarness();

            await TransitionToCleaningCluster(harness, prepared.Item2.Version);

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, _deployAttemptVersion);

            // Assert
            await harness.AssertDeployClusterRejected(_correlationId, _deployAttemptVersion);
        }
    }
}
