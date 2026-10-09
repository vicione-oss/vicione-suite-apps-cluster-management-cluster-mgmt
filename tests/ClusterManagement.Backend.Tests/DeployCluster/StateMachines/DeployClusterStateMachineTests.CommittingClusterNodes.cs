using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using ClusterManagement.Tests.Tools;
using MassTransit.Testing;
using NSubstitute.ReceivedExtensions;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

public partial class DeployClusterStateMachineTests
{
    public class CommittingClusterNodes : DeployClusterStateMachineTests
    {
        [Fact]
        public async Task Should_send_commit_cluster_command_if_options_contained_compressed_json()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var activeCluster = SetupSagaClusterActiveVersionOnly(provider);

            var commitVersion = _activeVersion.GetVersionIncrement(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, null, options);
            await harness.AssertDeployClusterState(_correlationId, x => x.CommittingCluster);

            // Assert            
            _snapshotStore.Received(0).Delete(_clusterId, commitVersion);
            await harness.Sent.Any<CommitClusterChange>(k => k.Context.Message.MetaInfo.ClusterId == _clusterId, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task Should_reject_deploy_cluster_if_saga_is_committing_cluster()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            SetupSagaClusterActiveVersionOnly(provider);
            var commitVersion = _activeVersion.GetVersionIncrement(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            var harness = provider.GetTestHarness();
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, null, options);
            await harness.AssertDeployClusterState(_correlationId, x => x.CommittingCluster);

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, _deployAttemptVersion);

            // Assert
            await harness.AssertDeployClusterRejected(_correlationId, _deployAttemptVersion);
        }

        [Fact]
        public async Task Should_fail_on_commit_cluster_failed()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var activeCluster = SetupSagaClusterActiveVersionOnly(provider);
            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, commitVersion, options);
            await harness.PublishClusterChangeFailed(_correlationId, _clusterId, commitVersion);

            // Assert
            await harness.AssertClusterUpdateFailed(_correlationId, commitVersion);
            Assert.True(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.Message.Reason == "TestFailure", TestContext.Current.CancellationToken),
                "Commit error should be the failure reason");
        }

        [Fact]
        public async Task Should_do_transition_to_adding_cluster_nodes_when_commit_was_successfull()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var activeCluster = provider.PrepareActiveClusterSaga(_snapshotStore, (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _activeVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                // Different node id so we'll have a node to be created in the commit cluster
                AddClusterApplication(group, _nodeIdToClean, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            });
            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, null, options);
            await harness.PublishClusterChangedCreated(_correlationId, _clusterId, commitVersion);

            // Assert            
            await harness.AssertDeployClusterState(_correlationId, x => x.AddingClusterNodes);
        }

        [Fact]
        public async Task Should_do_transition_to_updating_deployments_when_commit_was_successfull()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var activeCluster = SetupSagaClusterActiveVersionOnly(provider);
            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, commitVersion, options);
            await harness.PublishClusterChangedCreated(_correlationId, _clusterId, commitVersion);

            // Assert            
            await harness.AssertDeployClusterState(_correlationId, x => x.UpdatingDeployments);
        }
    }
}
