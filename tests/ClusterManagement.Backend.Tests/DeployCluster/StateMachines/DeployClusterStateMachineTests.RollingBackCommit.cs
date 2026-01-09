using AwesomeAssertions;
using ClusterManagement.Backend.DeployCluster.Extensions;
using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using MassTransit.Testing;
using Sdk.Messaging;
using Xunit;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

public partial class DeployClusterStateMachineTests
{
    public class RollingBackCommit : DeployClusterStateMachineTests
    {
        private async Task TransitionToRollingBackCommit(ITestHarness harness, DeployClusterOptions options, Version commitVersion)
        {
            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, null, options);
            await harness.PublishClusterChangedCreated(_correlationId, _clusterId, commitVersion);
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_correlationId, _clusterId, _applicationId);
            await harness.AssertDeployClusterState(_correlationId, x => x.RollingBackDeployments);
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId);
        }

        [Fact]
        public async Task Should_consume_change_event_if_committed_cluster_was_deleted()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            SetupSagaClusterActiveVersionOnly(provider);

            var harness = provider.GetTestHarness();

            await TransitionToRollingBackCommit(harness, options, commitVersion);

            // Act
            await harness.PublishClusterChangedDeleted(_correlationId, _clusterId, commitVersion);

            // Assert
            await harness.Consumed.Any<ClusterChanged>(k => k.Context.Message.ClusterId == _clusterId
                && k.Context.Message.Action == CrudAction.Created
                && k.Context.Message.Version == commitVersion);

            await harness.AssertClusterUpdateFailed(_correlationId, commitVersion);
        }

        [Fact]
        public async Task Should_ignore_unrelated_change_events()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            SetupSagaClusterActiveVersionOnly(provider);

            var harness = provider.GetTestHarness();

            await TransitionToRollingBackCommit(harness, options, commitVersion);

            // Act
            await harness.PublishClusterChangedDeleted(_correlationId, _clusterId, new Version(2, 2, 2));

            // Assert
            Assert.False(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.CorrelationId == _clusterId));
            (await harness.GetDeployClusterState()).IsCommitVersionEmpty().Should().BeFalse();
        }

        [Fact]
        public async Task Should_consume_error_event_if_committed_cluster_delete_failed()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            SetupSagaClusterActiveVersionOnly(provider);
            var harness = provider.GetTestHarness();

            await TransitionToRollingBackCommit(harness, options, commitVersion);

            // Act
            await harness.PublishClusterErrorDeleteFailed(_correlationId, _clusterId);

            // Assert
            await harness.Consumed.Any<ClusterError>(k => k.Context.Message.ClusterId == _clusterId);
            (await harness.GetDeployClusterState()).IsCommitVersionEmpty().Should().BeTrue();
        }

        [Fact]
        public async Task Should_ignore_unrelated_error_event_if_committed_cluster_delete_failed()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            SetupSagaClusterActiveVersionOnly(provider);
            var harness = provider.GetTestHarness();

            await TransitionToRollingBackCommit(harness, options, commitVersion);

            // Act
            await harness.Bus.Publish(new ClusterError
            {
                ClusterId = _clusterId,
                Error = new ErrorInfo(ClusterError.UnknownError, "Failure"),
                CorrelationId = _clusterId
            });

            // Assert
            Assert.False(await harness.Published.Any<ClusterUpdateFailed>(k => k.Context.CorrelationId == _clusterId));
            (await harness.GetDeployClusterState()).IsCommitVersionEmpty().Should().BeFalse();
        }

        [Fact]
        public async Task Finalize_when_commit_version_was_deleted()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            SetupSagaClusterActiveVersionOnly(provider);

            var harness = provider.GetTestHarness();

            await TransitionToRollingBackCommit(harness, options, commitVersion);

            // Act
            await harness.PublishClusterChangedDeleted(_correlationId, _clusterId, commitVersion);

            // Assert
            await harness.AssertClusterUpdateFailed(_correlationId, commitVersion);
        }

        [Fact]
        public async Task Should_reject_deploy_cluster_if_saga_is_rolling_back()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            SetupSagaClusterActiveVersionOnly(provider);

            var harness = provider.GetTestHarness();
            await TransitionToRollingBackCommit(harness, options, commitVersion);

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, _deployAttemptVersion);

            // Assert
            await harness.AssertDeployClusterRejected(_correlationId, _deployAttemptVersion);
        }
    }
}
