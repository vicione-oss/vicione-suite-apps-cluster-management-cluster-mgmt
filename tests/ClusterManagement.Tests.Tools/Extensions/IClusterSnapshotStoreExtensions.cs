using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public;
using NSubstitute;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Tests.Tools.Extensions;

public static class IClusterSnapshotStoreExtensions
{
    public static void SetupLoad(this IClusterSnapshotStore snapshotStore, Cluster cluster)
    {
        snapshotStore
            .Exists(cluster.Id, cluster.Version)
            .Returns(true);

        snapshotStore
            .Load(cluster.Id, cluster.Version, Arg.Any<CancellationToken>())
            .Returns(cluster);

        snapshotStore
            .LoadCompressedJson(cluster.Id, cluster.Version, Arg.Any<CancellationToken>())
            .Returns(ClusterSerializer.Compress(cluster));
    }
}
