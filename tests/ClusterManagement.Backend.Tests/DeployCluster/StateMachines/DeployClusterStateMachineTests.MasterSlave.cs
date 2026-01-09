using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Tests.Tools;
using MassTransit.Testing;
using ViciOne.Cluster.Model;
using Xunit;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

public partial class DeployClusterStateMachineTests
{

    public class DeployMasterSlaveCluster : DeployClusterStateMachineTests
    {
        private readonly Guid _slave1NodeId = Guid.Parse("BBBBBBBB-1111-414A-90BE-6F4BA877A9CF");
        private readonly Guid _slave1ApplicationId = Guid.Parse("BBBBBBBB-2222-414A-90BE-6F4BA877A9CF");

        private readonly Guid _slave2NodeId = Guid.Parse("CCCCCCCC-1111-414A-90BE-6F4BA877A9CF");
        private readonly Guid _slave2ApplicationId = Guid.Parse("CCCCCCCC-2222-414A-90BE-6F4BA877A9CF");

        [Fact]
        public async Task Saga_with_slaves_should_do_transitions_from_init_to_completed()
        {
            // Arrange
            await using var provider = SetupServiceProvider();

            var prepared = SetupSagaDbClusterLocal(provider, _snapshotStore);
            var harness = provider.GetTestHarness();

            await harness.Start();

            // Act
            await harness.DeployCluster(_clusterId, prepared.Item2.Version);
            await harness.PublishClusterNodeAdded(_clusterId, _slave2NodeId);

            await harness.PublishClusterApplicationStarted(_clusterId, _slave1ApplicationId);
            await harness.PublishClusterApplicationStarted(_clusterId, _slave2ApplicationId);

            await harness.PublishClusterNodeDeploymentsUpdated(_clusterId, _slave1ApplicationId);
            await harness.PublishClusterNodeDeploymentsUpdated(_clusterId, _slave2ApplicationId);

            await harness.PublishClusterNodeCleaned(_clusterId, _nodeIdToClean);

            // Assert
            await harness.AssertClusterUpdateCompleted(_clusterId, prepared.Item2.Version);
        }

        private (Cluster, Cluster) SetupSagaDbClusterLocal(IServiceProvider services, IClusterSnapshotStore store)
            => services.PrepareDeployClusterSaga(store, (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _activeVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _slave1NodeId, _slave1ApplicationId, ClusterApplicationType.CoreOsSlave);

                return cluster;
            },
            (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _targetVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _slave1NodeId, _slave1ApplicationId, ClusterApplicationType.CoreOsSlave);
                AddClusterApplication(group, _slave2NodeId, _slave2ApplicationId, ClusterApplicationType.CoreOsSlave);

                return cluster;
            });
    }
}
