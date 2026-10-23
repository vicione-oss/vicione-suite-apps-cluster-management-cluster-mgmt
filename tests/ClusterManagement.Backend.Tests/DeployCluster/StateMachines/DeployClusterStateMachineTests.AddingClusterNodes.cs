using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
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
        public async Task Should_clean_the_created_nodes_if_target_cluster_json_cannot_be_loaded_after_nodes_were_added()
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
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackClusterNodes);
            await harness.PublishClusterNodeCleaned(_correlationId, _clusterId, _nodeIdToAdd);

            // Assert
            Assert.True(await harness.Sent.Any<CleanClusterNodesCommand>(k => k.Context.Message.NodesToClean.Contains(_nodeIdToAdd), TestContext.Current.CancellationToken),
                "The created node should be cleaned");
            Assert.False(await harness.Published.Any<UpdateDeployments>(TestContext.Current.CancellationToken),
                "Nothing was published, so no deployment should be rolled back");
            Assert.True(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.Message.Version == prepared.Item2.Version
                && k.Context.Message.Reason == "Snapshot not found", TestContext.Current.CancellationToken),
                "The failure should report the original reason");
            await AssertSagaFinalized(harness);
        }

        [Fact]
        public async Task Should_clean_the_created_nodes_and_remove_the_committed_version_if_target_cluster_json_cannot_be_loaded()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            SetupSagaClusterActiveVersionOnNodeToClean(provider);
            _snapshotStore.LoadCompressedJson(_clusterId, commitVersion, Arg.Any<CancellationToken>())
                .Returns(Task.FromException<byte[]>(new InvalidOperationException("Snapshot not found")));
            var harness = provider.GetTestHarness();

            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, null, options);
            await harness.PublishClusterChangedCreated(_correlationId, _clusterId, commitVersion);
            await harness.AssertDeployClusterState(_correlationId, x => x.AddingClusterNodes);

            // Act
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackClusterNodes);
            await harness.PublishClusterNodeCleaned(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackCommit);
            await harness.PublishClusterChangedDeleted(_correlationId, _clusterId, commitVersion);

            // Assert
            Assert.True(await harness.Sent.Any<RemoveCluster>(k => k.Context.Message.Version == commitVersion, TestContext.Current.CancellationToken),
                "The committed version should be removed");
            Assert.False(await harness.Published.Any<UpdateDeployments>(TestContext.Current.CancellationToken),
                "Nothing was published, so no deployment should be rolled back");
            Assert.True(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.Message.Version == commitVersion
                && k.Context.Message.Reason == "Snapshot not found", TestContext.Current.CancellationToken),
                "The failure should report the committed version and the original reason");
            await AssertSagaFinalized(harness);
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
