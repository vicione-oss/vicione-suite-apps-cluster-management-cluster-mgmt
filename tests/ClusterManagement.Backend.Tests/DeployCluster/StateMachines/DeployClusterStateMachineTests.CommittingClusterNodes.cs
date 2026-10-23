using ClusterManagement.Backend.DeployCluster.Contracts;
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
        /// <summary>
        /// Deploys with a commit and replies that the commit was created; <paramref name="failAfterCommit"/> sets up the step that fails next.
        /// </summary>
        private async Task<Version> DeployAndFailAfterCommit(IServiceProvider provider, Action<Version> failAfterCommit)
        {
            SetupSagaClusterActiveVersionOnly(provider);
            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            failAfterCommit(commitVersion);
            var harness = provider.GetTestHarness();
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, null, options);
            await harness.PublishClusterChangedCreated(_correlationId, _clusterId, commitVersion);
            return commitVersion;
        }

        private void FailLoadingCommittedCluster(Version commitVersion)
            => _snapshotStore.Load(_clusterId, commitVersion, Arg.Any<CancellationToken>())
                .Returns<Cluster?>(_ => throw new InvalidOperationException("Load failed"));

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
        public async Task Should_report_the_commit_version_as_target_while_committing_cluster()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            SetupSagaClusterActiveVersionOnly(provider);
            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            var harness = provider.GetTestHarness();
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, null, options);
            await harness.AssertDeployClusterState(_correlationId, x => x.CommittingCluster);

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, _deployAttemptVersion);

            // Assert
            Assert.True(await harness.Published.Any<ClusterUpdateChanged>(k => k.Context.Message.CurrentState == "CommittingCluster"
                && k.Context.Message.TargetVersion == commitVersion, TestContext.Current.CancellationToken),
                "Transition to CommittingCluster should report the commit version");
            Assert.True(await harness.Published.Any<ClusterUpdateRejected>(k => k.Context.Message.RejectedVersion == _deployAttemptVersion
                && k.Context.Message.TargetVersion == commitVersion, TestContext.Current.CancellationToken),
                "Rejection should report the commit version as the running target");
        }

        [Fact]
        public async Task Should_fail_with_the_commit_version_if_the_commit_reply_is_unexpected()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            SetupSagaClusterActiveVersionOnly(provider);
            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            var harness = provider.GetTestHarness();
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, null, options);
            await harness.AssertDeployClusterState(_correlationId, x => x.CommittingCluster);

            // Act
            await harness.PublishClusterChangedDeleted(_correlationId, _clusterId, commitVersion);

            // Assert
            await harness.AssertClusterUpdateFailed(_correlationId, commitVersion);
            Assert.False(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.Message.Version == new Version(0, 0, 0), TestContext.Current.CancellationToken),
                "No failure should report version 0.0.0");
            Assert.False(await harness.Sent.Any<RemoveCluster>(TestContext.Current.CancellationToken),
                "Nothing was committed, so nothing should be removed");
        }

        [Fact]
        public async Task Should_fail_without_removing_the_commit_version_if_another_version_was_created()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            SetupSagaClusterActiveVersionOnly(provider);
            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            var otherVersion = new Version(commitVersion.Major, commitVersion.Minor, commitVersion.Build + 1);
            var harness = provider.GetTestHarness();
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, null, options);
            await harness.AssertDeployClusterState(_correlationId, x => x.CommittingCluster);

            // Act
            await harness.PublishClusterChangedCreated(_correlationId, _clusterId, otherVersion);

            // Assert
            Assert.True(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.CorrelationId == _correlationId
                && k.Context.Message.Version == commitVersion
                && k.Context.Message.State == "CommittingCluster"
                && !k.Context.Message.FailedVersionKept, TestContext.Current.CancellationToken),
                "The failure should report the commit version without a failed rollback");
            Assert.False(await harness.Sent.Any<RemoveCluster>(TestContext.Current.CancellationToken),
                "The commit version may belong to another commit, so it should not be removed");
        }

        [Fact]
        public async Task Should_remove_the_committed_version_if_the_deployment_fails_after_the_commit()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var harness = provider.GetTestHarness();

            // Act
            var commitVersion = await DeployAndFailAfterCommit(provider, FailLoadingCommittedCluster);

            // Assert
            Assert.True(await harness.Sent.Any<RemoveCluster>(k => k.Context.Message.ClusterId == _clusterId
                && k.Context.Message.Version == commitVersion, TestContext.Current.CancellationToken),
                "The committed version should be removed");
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackCommit);
            Assert.False(await harness.Published.Any<ClusterUpdateFailed>(TestContext.Current.CancellationToken),
                "The failure should be published once the committed version is removed");
        }

        [Fact]
        public async Task Should_remove_the_committed_version_if_updating_the_deployments_fails_after_the_commit()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var harness = provider.GetTestHarness();

            // Act
            var commitVersion = await DeployAndFailAfterCommit(provider, version => _snapshotStore
                .LoadCompressedJson(_clusterId, version, Arg.Any<CancellationToken>())
                .Returns<byte[]>(_ => throw new InvalidOperationException("Load compressed failed")));
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackCommit);
            await harness.PublishClusterChangedDeleted(_correlationId, _clusterId, commitVersion);

            // Assert
            Assert.True(await harness.Sent.Any<RemoveCluster>(k => k.Context.Message.Version == commitVersion, TestContext.Current.CancellationToken),
                "The committed version should be removed");
            Assert.False(await harness.Published.Any<UpdateDeployments>(TestContext.Current.CancellationToken),
                "No deployment should be updated");
            Assert.True(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.Message.Version == commitVersion
                && k.Context.Message.Reason == "Load compressed failed", TestContext.Current.CancellationToken),
                "The failure should report the committed version and the original reason");
        }

        [Fact]
        public async Task Should_fail_with_the_original_reason_once_the_committed_version_is_removed()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var harness = provider.GetTestHarness();
            var commitVersion = await DeployAndFailAfterCommit(provider, FailLoadingCommittedCluster);
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackCommit);

            // Act
            await harness.PublishClusterChangedDeleted(_correlationId, _clusterId, commitVersion);

            // Assert
            Assert.True(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.CorrelationId == _correlationId
                && k.Context.Message.Version == commitVersion
                && k.Context.Message.Reason == "Load failed"
                && k.Context.Message.State == "RollingBackCommit", TestContext.Current.CancellationToken),
                "The failure should report the committed version, the original reason and the rollback state");
        }

        [Fact]
        public async Task Should_fail_with_the_original_reason_if_removing_the_committed_version_failed()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var harness = provider.GetTestHarness();
            var commitVersion = await DeployAndFailAfterCommit(provider, FailLoadingCommittedCluster);
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackCommit);

            // Act
            await harness.PublishClusterErrorDeleteFailed(_correlationId, _clusterId, commitVersion);

            // Assert
            Assert.True(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.CorrelationId == _correlationId
                && k.Context.Message.Version == commitVersion
                && k.Context.Message.Reason == "Load failed"
                && k.Context.Message.State == "RollingBackCommit"
                && k.Context.Message.FailedVersionKept, TestContext.Current.CancellationToken),
                "The failure should report a failed rollback that kept the committed version, with the original reason");
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
            await AssertSagaFinalized(harness);
            AssertCommitVersionReleased(provider, commitVersion);
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
