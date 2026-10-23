using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
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
        public async Task Should_keep_the_created_nodes_if_active_cluster_json_cannot_be_loaded_for_rollback()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            _snapshotStore.LoadCompressedJson(_clusterId, prepared.Item1!.Version, Arg.Any<CancellationToken>())
                .Returns(Task.FromException<byte[]>(new InvalidOperationException("Snapshot not found")));
            var harness = provider.GetTestHarness();
            await TransitionToUpdatingDeployments(harness, prepared.Item2.Version);

            // Act: the rollback to the active version cannot start
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_correlationId, _clusterId, _applicationId, prepared.Item2.Version);

            // Assert
            await AssertSagaFinalized(harness);
            Assert.False(await harness.Sent.Any<CleanClusterNodesCommand>(TestContext.Current.CancellationToken),
                "The applications may still run the target version, so its nodes must not be cleaned");
            Assert.True(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.Message.Version == prepared.Item2.Version
                && k.Context.Message.Reason == "At least one application update failed"
                && k.Context.Message.ErrorsByApplication.ContainsKey(_applicationId), TestContext.Current.CancellationToken),
                "The failure should report the original reason and the error of the failed application");
        }

        [Fact]
        public async Task Should_keep_the_committed_version_if_the_deployments_cannot_be_rolled_back()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            SetupSagaClusterActiveVersionOnNodeToClean(provider);
            _snapshotStore.LoadCompressedJson(_clusterId, _activeVersion, Arg.Any<CancellationToken>())
                .Returns(Task.FromException<byte[]>(new InvalidOperationException("Snapshot not found")));
            var harness = provider.GetTestHarness();
            await TransitionToUpdatingDeployments(harness, commitVersion, options);

            // Act: the rollback to the active version cannot start
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_correlationId, _clusterId, _applicationId, commitVersion);

            // Assert
            await AssertSagaFinalized(harness);
            Assert.False(await harness.Sent.Any<RemoveCluster>(k => k.Context.Message.Version == commitVersion, TestContext.Current.CancellationToken),
                "The applications may still run the committed version, so it must not be removed");
            Assert.False(await harness.Sent.Any<CleanClusterNodesCommand>(TestContext.Current.CancellationToken),
                "The applications may still run the committed version, so its nodes must not be cleaned");
            Assert.True(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.Message.Version == commitVersion
                && k.Context.Message.FailedVersionKept
                && k.Context.Message.ErrorsByApplication.ContainsKey(_applicationId), TestContext.Current.CancellationToken),
                "The failure should report the kept version and the error of the failed application");
        }

        [Fact]
        public async Task Should_roll_back_and_remove_the_committed_version_if_the_target_version_cannot_be_activated()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            SetupSagaClusterActiveVersionOnNodeToClean(provider);
            FailActiveClusterUpdate(provider);
            var harness = provider.GetTestHarness();
            await TransitionToUpdatingDeployments(harness, commitVersion, options);

            // Act
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId, commitVersion);
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackDeployments);
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId, _activeVersion);
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackClusterNodes);
            await harness.PublishClusterNodeCleaned(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackCommit);
            await harness.PublishClusterChangedDeleted(_correlationId, _clusterId, commitVersion);

            // Assert
            Assert.True(await harness.Published.Any<UpdateDeployments>(k => k.Context.Message.ClusterVersion == _activeVersion, TestContext.Current.CancellationToken),
                "The deployments should be rolled back to the active version");
            Assert.True(await harness.Sent.Any<RemoveCluster>(k => k.Context.Message.Version == commitVersion, TestContext.Current.CancellationToken),
                "The committed version should be removed");
            Assert.True(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.Message.Version == commitVersion
                && IsActivationFailure(k.Context.Message.Reason), TestContext.Current.CancellationToken),
                "The failure should report the committed version and the original reason");
            await AssertSagaFinalized(harness);
        }

        [Fact]
        public async Task Should_roll_back_if_the_target_version_cannot_be_activated()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            FailActiveClusterUpdate(provider);
            var harness = provider.GetTestHarness();
            await TransitionToUpdatingDeployments(harness, prepared.Item2.Version);

            // Act
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId, prepared.Item2.Version);
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackDeployments);
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId, _activeVersion);
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackClusterNodes);
            await harness.PublishClusterNodeCleaned(_correlationId, _clusterId, _nodeIdToAdd);

            // Assert
            Assert.True(await harness.Published.Any<UpdateDeployments>(k => k.Context.Message.ClusterVersion == _activeVersion, TestContext.Current.CancellationToken),
                "The deployments should be rolled back to the active version");
            Assert.True(await harness.Sent.Any<CleanClusterNodesCommand>(k => k.Context.Message.NodesToClean.Contains(_nodeIdToAdd), TestContext.Current.CancellationToken),
                "The created node should be cleaned");
            Assert.True(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.Message.Version == prepared.Item2.Version
                && IsActivationFailure(k.Context.Message.Reason), TestContext.Current.CancellationToken),
                "The failure should report the original reason");
            await AssertSagaFinalized(harness);
            GetActiveVersion(provider).Should().Be(_activeVersion, "The active version should be untouched");
        }

        [Fact]
        public async Task Should_fail_a_redeploy_of_the_active_version_if_an_application_failed_and_the_rollback_cannot_start()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            SetupSagaClusterActiveVersionOnly(provider);
            var harness = provider.GetTestHarness();
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, _activeVersion);
            await harness.AssertDeployClusterState(_correlationId, x => x.UpdatingDeployments);
            _snapshotStore.LoadCompressedJson(_clusterId, _activeVersion, Arg.Any<CancellationToken>())
                .Returns(Task.FromException<byte[]>(new InvalidOperationException("Snapshot not found")));

            // Act
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_correlationId, _clusterId, _applicationId, _activeVersion);

            // Assert
            await AssertSagaFinalized(harness);
            Assert.True(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.Message.Version == _activeVersion
                && k.Context.Message.Reason == "At least one application update failed", TestContext.Current.CancellationToken),
                "An application failed, so the redeploy should fail");
            Assert.False(await harness.Published.Any<ClusterUpdateCompleted>(TestContext.Current.CancellationToken),
                "An application failed, so the redeploy should not complete");
        }

        [Fact]
        public async Task Should_complete_a_redeploy_of_the_active_version_if_activating_it_throws()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            SetupSagaClusterActiveVersionOnly(provider);
            FailActiveClusterUpdate(provider);
            var harness = provider.GetTestHarness();
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, _activeVersion);
            await harness.AssertDeployClusterState(_correlationId, x => x.UpdatingDeployments);

            // Act: every application runs the active version again
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId, _activeVersion);

            // Assert
            await harness.AssertClusterUpdateCompleted(_correlationId, _activeVersion);
            await AssertSagaFinalized(harness);
            Assert.False(await harness.Published.Any<ClusterUpdateFailed>(TestContext.Current.CancellationToken),
                "The version being deployed is active already");
        }

        /// <summary>
        /// The reason <see cref="FailActiveClusterUpdate"/> causes, which EF reports in its own words.
        /// </summary>
        private static bool IsActivationFailure(string? reason)
            => !string.IsNullOrEmpty(reason) && reason != "At least one application update failed";

        /// <summary>
        /// Reads the stored active version, not the one tracked by the shared context, which keeps a failed update.
        /// </summary>
        private Version GetActiveVersion(IServiceProvider provider)
            => provider.GetRequiredService<IClusterDbContext>().ActiveClusters.AsNoTracking().Single(k => k.Id == _clusterId).Version;

        private static void FailActiveClusterUpdate(IServiceProvider provider)
        {
            var dbContext = (Microsoft.EntityFrameworkCore.DbContext)provider.GetRequiredService<IClusterDbContext>();
            var table = dbContext.Model.FindEntityType(typeof(ActiveClusterInfo))!.GetTableName();
            var sql = $"CREATE TRIGGER fail_active_cluster_update BEFORE UPDATE ON \"{table}\" BEGIN SELECT RAISE(ABORT, 'Activation failed'); END;";
            dbContext.Database.ExecuteSqlRaw(sql);
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
