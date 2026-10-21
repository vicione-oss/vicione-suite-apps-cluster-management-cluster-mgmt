using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Enums;

namespace ClusterManagement.Backend.Extensions;

internal static class IClusterSnapshotStoreExtensions
{
    public static async Task<Version> GetLatestVersion(this IClusterSnapshotStore snapshotStore, Guid clusterId, CancellationToken cancellationToken = default)
    {
        var allVersions = await snapshotStore.GetClusterInfos(clusterId, cancellationToken);
        return allVersions.Max(k => k.Version) ?? new Version(0, 0, 1);
    }

    /// <summary>
    /// The next version after both the stored and the reserved versions of the cluster.
    /// Call it holding the <paramref name="clusterLock"/> of the cluster, until the version is stored or reserved.
    /// </summary>
    public static async Task<Version> GetLatestVersionIncrement(this IClusterSnapshotStore snapshotStore, IClusterLock clusterLock, Guid clusterId, VersionIncrementKind updateType, CancellationToken cancellationToken = default)
    {
        var latestVersion = await snapshotStore.GetLatestVersion(clusterId, cancellationToken);
        var latestReserved = clusterLock.GetReservedVersions(clusterId).Max();
        return (latestReserved > latestVersion ? latestReserved : latestVersion).GetVersionIncrement(updateType);
    }
}
