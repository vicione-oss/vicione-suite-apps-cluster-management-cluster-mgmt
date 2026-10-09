using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Public.Enums;
using ClusterManagement.Tests.Tools;
using MassTransit.Testing;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

public partial class DeployClusterStateMachineTests
{
    public class DeployStandaloneCluster : DeployClusterStateMachineTests
    {
        [Fact]
        public async Task Should_complete_node_application_deployment()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId);
            await harness.PublishClusterNodeCleaned(_correlationId, _clusterId, _nodeIdToClean);

            // Assert
            await harness.AssertClusterUpdateCompleted(_correlationId, prepared.Item2.Version);
        }

        [Fact]
        public async Task Should_fail_if_at_least_one_deployment_failed()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);

            // -> trigger rollback
            await harness.PublishClusterNodeDeploymentsUpdateFailed(_correlationId, _clusterId, _applicationId);

            // -> complete rollback
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId);

            // -> remove the created node
            await harness.PublishClusterNodeCleaned(_correlationId, _clusterId, _nodeIdToAdd);

            // Assert
            await harness.AssertClusterUpdateFailed(_correlationId, prepared.Item2.Version);
        }

        [Fact]
        public async Task Should_process_target_cluster_from_command_options()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var activeCluster = SetupSagaClusterActiveVersionOnly(provider);

            var commitVersion = SetupCommitClusterInfos(VersionIncrementKind.Patch);
            var options = SetupDeployOptions(commitVersion);
            var harness = provider.GetTestHarness();
            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, null, options);
            await harness.PublishClusterChangedCreated(_correlationId, _clusterId, commitVersion);
            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId);
            await harness.PublishClusterNodeCleaned(_correlationId, _clusterId, _nodeIdToClean);

            // Assert
            await harness.AssertClusterUpdateCompleted(_correlationId, null);
        }

        [Fact]
        public async Task Should_deploy_different_cluster_parallel()
        {
            // Arrange
            await using var provider = SetupServiceProvider();
            var otherCorrelationId = Guid.NewGuid();
            var otherClusterId = Guid.NewGuid();
            var otherClusterActiveVersion = new Version(1, 2, 5);
            var otherClusterTargetVersion = new Version(1, 5, 3);
            var otherNodeGroupId = Guid.NewGuid();
            var otherNodeIdToAdd = Guid.NewGuid();
            var otherNodeIdToClean = Guid.NewGuid();
            var otherApplicationIdId = Guid.NewGuid();

            var prepared = SetupSagaClusterWithExistingTargetVersion(provider, _snapshotStore);
            var otherPrepared = provider.PrepareDeployClusterSaga(_snapshotStore, (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(otherClusterId, otherClusterActiveVersion);
                var group = AddNodeGroup(cluster, otherNodeGroupId);

                AddClusterApplication(group, otherNodeIdToClean, otherApplicationIdId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            },
            (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(otherClusterId, otherClusterTargetVersion);
                var group = AddNodeGroup(cluster, otherNodeGroupId);

                AddClusterApplication(group, otherNodeIdToAdd, otherApplicationIdId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            });

            var harness = provider.GetTestHarness();

            await harness.Start();

            // Act
            await harness.DeployCluster(_correlationId, _clusterId, prepared.Item2.Version);
            await harness.DeployCluster(otherCorrelationId, otherClusterId, otherPrepared.Item2.Version);

            await harness.PublishClusterNodeAdded(_correlationId, _clusterId, _nodeIdToAdd);
            await harness.PublishClusterNodeAdded(otherCorrelationId, otherClusterId, otherNodeIdToAdd);

            await harness.PublishClusterApplicationStarted(_correlationId, _clusterId, _applicationId);
            await harness.PublishClusterApplicationStarted(otherCorrelationId, otherClusterId, otherApplicationIdId);

            await harness.PublishClusterNodeDeploymentsUpdated(_correlationId, _clusterId, _applicationId);
            await harness.PublishClusterNodeDeploymentsUpdated(otherCorrelationId, otherClusterId, otherApplicationIdId);

            await harness.PublishClusterNodeCleaned(_correlationId, _clusterId, _nodeIdToClean);
            await harness.PublishClusterNodeCleaned(otherCorrelationId, otherClusterId, otherNodeIdToClean);

            // Assert
            await harness.AssertClusterUpdateCompleted(_correlationId, prepared.Item2.Version);
            await harness.AssertClusterUpdateCompleted(otherCorrelationId, otherPrepared.Item2.Version);
        }
    }
}
