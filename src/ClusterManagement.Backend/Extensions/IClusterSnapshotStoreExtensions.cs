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

    public static async Task<Version> GetLatestVersionIncrement(this IClusterSnapshotStore snapshotStore, Guid clusterId, VersionIncrementKind updateType, CancellationToken cancellationToken = default)
    {
        var latestVersion = await snapshotStore.GetLatestVersion(clusterId, cancellationToken);
        return latestVersion.GetVersionIncrement(updateType);
    }
}
