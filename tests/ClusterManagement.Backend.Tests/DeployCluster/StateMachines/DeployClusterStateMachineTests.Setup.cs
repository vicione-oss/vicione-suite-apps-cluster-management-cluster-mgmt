using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Backend.Tests.Extensions;
using ClusterManagement.Public;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Requests;
using ClusterManagement.Tests.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

public partial class DeployClusterStateMachineTests
{
    private readonly IClusterSnapshotStore _snapshotStore = Substitute.For<IClusterSnapshotStore>();
    private readonly ILogger<DeployClusterStateMachine> _logger = Substitute.For<ILogger<DeployClusterStateMachine>>();
    // Arrange: create a FakeLogger for MyService
    //var fakeLogger = new FakeLogger<MyService>();

    private readonly Guid _correlationId = Guid.Parse("ADADADAD-C223-42AC-95C1-1C3D2F36D171");
    private readonly Guid _clusterId = Guid.Parse("BCBCBCBC-C223-42AC-95C1-1C3D2F36D171");
    private readonly Version _activeVersion = new(1, 0, 0);
    private readonly Version _targetVersion = new(1, 2, 1);
    private readonly Version _deployAttemptVersion = new(1, 4, 1);

    private readonly Guid _nodeGroupId = Guid.NewGuid();
    private readonly Guid _nodeIdToClean = Guid.Parse("00000000-1111-414A-90BE-6F4BA877A9CF");
    private readonly Guid _nodeIdToAdd = Guid.Parse("AAAAAAAA-1111-414A-90BE-6F4BA877A9CF");
    private readonly Guid _applicationId = Guid.Parse("AAAAAAAA-2222-414A-90BE-6F4BA877A9CF");

    private ServiceProvider SetupServiceProvider()
        => new ServiceCollection()
            .ConfigureDeployClusterSaga(_snapshotStore, x =>
            {
                x.AddSagaStateMachine<DeployClusterStateMachine, DeployClusterState>();
                x.AddSingleton(_logger);
            })
            .BuildServiceProvider(true);

    private Cluster SetupSagaClusterActiveVersionOnly(IServiceProvider services)
            => services.PrepareActiveClusterSaga(_snapshotStore, (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _activeVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            });

    private (Cluster, Cluster) SetupSagaClusterWithExistingTargetVersion(IServiceProvider services, IClusterSnapshotStore store)
            => services.PrepareDeployClusterSaga(store, (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _activeVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToClean, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            },
            (_) =>
            {
                var cluster = ClusterDbContextTools.CreateCluster(_clusterId, _targetVersion);
                var group = AddNodeGroup(cluster, _nodeGroupId);

                AddClusterApplication(group, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

                return cluster;
            });

    private (Cluster, Cluster) SetupSagaClusterWithTargetOnSameNode(IServiceProvider services)
        => services.PrepareDeployClusterSaga(_snapshotStore, (_) =>
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

    private DeployClusterOptions SetupDeployOptions(Version targetVersion)
    {
        var targetCluster = ClusterDbContextTools.CreateCluster(_clusterId, targetVersion);
        var targetGroup = AddNodeGroup(targetCluster, _nodeGroupId);
        AddClusterApplication(targetGroup, _nodeIdToAdd, _applicationId, ClusterApplicationType.CoreOsStandalone);

        _snapshotStore.Load(_clusterId, targetVersion, Arg.Any<CancellationToken>())
            .Returns(targetCluster);

        var targetCompressed = ClusterSerializer.Compress(targetCluster);

        _snapshotStore.LoadCompressedJson(_clusterId, targetVersion, Arg.Any<CancellationToken>())
            .Returns(targetCompressed);

        return new DeployClusterOptions(targetCompressed);
    }

    private Version SetupCommitClusterInfos(VersionIncrementKind incrementKind, IEnumerable<ClusterInfo>? additionalClusters = null)
    {
        var infos = new List<ClusterInfo>() {
            new(_clusterId, _activeVersion, "Test", true)
        };

        if (additionalClusters is not null)
            infos.AddRange(additionalClusters);

        _snapshotStore.GetClusterInfos(_clusterId, Arg.Any<CancellationToken>()).Returns(infos);

        var latest = infos.Max(k => k.Version) ?? throw new InvalidOperationException("Can't determine max cluster version");

        return latest.GetVersionIncrement(incrementKind);
    }

    private static ClusterNodeGroup AddNodeGroup(Cluster cluster, Guid groupId)
    {
        var group = new ClusterNodeGroup
        {
            Id = groupId
        };
        cluster.NodeGroups.Add(group);
        return group;
    }

    private static void AddClusterApplication(ClusterNodeGroup group, Guid nodeId, Guid applicationId, ClusterApplicationType type) => group.Nodes.Add(new ClusterNode
    {
        Id = nodeId,
        Applications =
        {
            new ClusterApplication
            {
                Id = applicationId,
                Type = type,
            },
        },
    });
}
