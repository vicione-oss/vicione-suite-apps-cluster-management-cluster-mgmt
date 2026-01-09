using AwesomeAssertions;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Enums;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

public partial class DeployClusterStateMachineTests
{
    public class RollingBackDeployments : DeployClusterStateMachineTests
    {
        private async Task TransitionRollingBackDeployments(ITestHarness harness, DeployClusterOptions options, Version commitVersion)
        {
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, null, options);
            await harness.PublishClusterChangedCreated(_correlationId, _clusterId, commitVersion);
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_correlationId, _clusterId, _applicationId);
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackDeployments);
        }

        private async Task TransitionRollingBackDeployments(ITestHarness harness, Version targetVersion)
        {
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, targetVersion);
            await harness.PublishClusterChangedCreated(_correlationId, _clusterId, targetVersion);
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_correlationId, _clusterId, _applicationId);
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackDeployments);
        }

        [Fact]
        public async Task Finalize_when_all_nodes_are_rolled_back()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await TransitionRollingBackDeployments(harness, prepared.Item2.Version);

            // Act
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId);

            // Assert
            await harness.AssertClusterUpdateFailed(_correlationId, prepared.Item2.Version);

            var dbContext = provider.GetRequiredService<IClusterDbContext>();
            dbContext.ActiveClusters.Single(k => k.Id == _clusterId).Version.Should().Be(prepared.Item1!.Version, "Active cluster should untouched");
        }

        [Fact]
        public async Task Finalize_when_nodes_rollback_failed()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await TransitionRollingBackDeployments(harness, prepared.Item2.Version);

            // Act
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_correlationId, _clusterId, _applicationId);

            // Assert
            await harness.AssertClusterUpdateFailed(_correlationId, prepared.Item2.Version);
        }

        [Fact]
        public async Task Transition_to_rollback_commit_if_commit_version_is_not_empty()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            SetupSagaClusterActiveVersionOnly(provider);
            var harness = provider.GetTestHarness();

            await TransitionRollingBackDeployments(harness, options, commitVersion);

            // Act
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId);

            // Assert
            await harness.Sent.Any<RemoveCluster>(k => k.Context.Message.ClusterId == _clusterId
                && k.Context.Message.Version == commitVersion);

            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackCommit);

            var dbContext = provider.GetRequiredService<IClusterDbContext>();
            dbContext.ActiveClusters.Single(k => k.Id == _clusterId).Version.Should().Be(_activeVersion, "Active cluster should untouched");
        }

        [Fact]
        public async Task Transition_to_rollback_commit_if_commit_version_is_not_empty_and_deployments_failed()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            SetupSagaClusterActiveVersionOnly(provider);
            var harness = provider.GetTestHarness();

            await TransitionRollingBackDeployments(harness, options, commitVersion);

            // Act
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_correlationId, _clusterId, _applicationId);

            // Assert            
            await harness.Sent.Any<RemoveCluster>(k => k.Context.Message.ClusterId == _clusterId
                && k.Context.Message.Version == commitVersion);

            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackCommit);
        }

        [Fact]
        public async Task Should_reject_deploy_cluster_if_saga_is_rolling_back()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await TransitionRollingBackDeployments(harness, prepared.Item2.Version);

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, _deployAttemptVersion);

            // Assert
            await harness.AssertDeployClusterRejected(_correlationId, _deployAttemptVersion);
        }
    }
}
