using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Requests;
using ClusterManagement.Tests.Tools;
using MassTransit.Testing;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

public partial class DeployClusterStateMachineTests
{
    public class Initially : DeployClusterStateMachineTests
    {
        [Fact]
        public async Task Should_initialize_saga_on_deploy_cluster_command()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);

            // Assert
            await harness.AssertDeployClusterState(_correlationId, x => x.AddingClusterNodes);
        }

        [Fact]
        public async Task Should_initialize_optional_target_cluster()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);

            // Assert
            await harness.AssertDeployClusterState(_correlationId, x => x.AddingClusterNodes);
        }

        [Fact]
        public async Task Transition_to_adding_cluster_nodes_if_nodes_need_to_be_created()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);

            // Assert
            await harness.AssertDeployClusterState(_correlationId, x => x.AddingClusterNodes);
        }

        [Fact]
        public async Task Should_do_transition_to_update_deployments_if_nodes_dont_need_to_be_created()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = provider.PrepareDeployClusterSaga(_snapshotStore, (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _activeVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            },
            (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _targetVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            });
            var harness = provider.GetTestHarness();

            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);

            // Assert
            await harness.AssertDeployClusterState(_correlationId, x => x.UpdatingDeployments);
        }

        [Fact]
        public async Task Should_fail_if_no_active_cluster_is_available()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, _targetVersion);

            // Assert
            await harness.AssertClusterUpdateFailed(_correlationId, null);
        }

        [Fact]
        public async Task Should_fail_if_no_target_cluster_is_available()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var activeCluster = SetupSagaClusterActiveVersionOnly(provider);

            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, _targetVersion);

            // Assert
            await harness.AssertClusterUpdateFailed(_correlationId, _targetVersion);
        }

        [Fact]
        public async Task Should_fail_if_options_cluster_is_invalid()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var activeCluster = SetupSagaClusterActiveVersionOnly(provider);

            var options = new DeployClusterOptions([0x20, 0x22, 0x25, 0x27, 0x16, 0x21]);
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, _targetVersion, options);
            await harness.PublishClusterChangeFailed(_correlationId, _clusterId, _targetVersion);

            // Assert
            await harness.AssertClusterUpdateFailed(_correlationId, null);
        }

        [Fact]
        public async Task Should_fail_if_no_target_version_or_options_are_set()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var activeCluster = SetupSagaClusterActiveVersionOnly(provider);

            var options = new DeployClusterOptions([0x20, 0x22, 0x25, 0x27, 0x16, 0x21]);
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, null, options);
            await harness.PublishClusterChangeFailed(_correlationId, _clusterId, _targetVersion);

            // Assert
            await harness.AssertClusterUpdateFailed(_correlationId, null);
        }

        [Fact]
        public async Task Should_use_commit_version_based_on_latest_version_increment()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var activeCluster = SetupSagaClusterActiveVersionOnly(provider);
            var clusterInfos = new List<ClusterInfo>
            {
                new(_clusterId, new Version(1,4,5), activeCluster.Name),
                new(_clusterId, new Version(1,4,7), activeCluster.Name),
                new(_clusterId, new Version(1,5,1), activeCluster.Name),
                new(_clusterId, new Version(1,6,3), activeCluster.Name),
            };

            var expectedVersion = SetupCommitClusterInfos(Public.Enums.VersionIncrementKind.Minor, clusterInfos);

            var options = new DeployClusterOptions([0x20, 0x22, 0x25, 0x27, 0x16, 0x21], Public.Enums.VersionIncrementKind.Minor);
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, null, options);

            // Assert
            Assert.True(await harness.Sent.Any<CommitClusterChange>(k => k.Context.Message.MetaInfo.ClusterVersion == expectedVersion, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_reject_deploy_cluster_if_saga_is_already_started()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, _deployAttemptVersion);

            // Assert
            await harness.AssertDeployClusterRejected(_correlationId, _deployAttemptVersion);
        }
    }
}
