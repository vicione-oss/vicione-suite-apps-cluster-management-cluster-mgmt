using System.Text.Json.Nodes;
using ClusterManagement.Public.Requests;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Snapshots;

/// <summary>
/// Provides storage and retrieval operations for cluster snapshots.
/// </summary>
public interface IClusterSnapshotStore
{
    /// <summary>
    /// Stores compressed cluster JSON with associated metadata.
    /// </summary>
    Task Store(byte[] compressedClusterJson, ClusterSnapshotInfo info, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a generic value with cluster metadata using a custom write function.
    /// </summary>
    Task Store<T>(Guid clusterId, Version clusterVersion, JsonObject info, T value, Func<Stream, T, CancellationToken, Task> write, bool replace, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a cluster snapshot to the file system.
    /// </summary>
    Task Store(Cluster cluster, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a cluster snapshot by ID and version.
    /// </summary>
    Task<Cluster?> Load(Guid clusterId, Version clusterVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the raw compressed JSON bytes for a cluster snapshot.
    /// </summary>
    Task<byte[]> LoadCompressedJson(Guid clusterId, Version clusterVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a cluster snapshot using a custom read function.
    /// </summary>
    Task<T?> Load<T>(Guid clusterId, Version clusterVersion, Func<Stream, CancellationToken, Task<T>> read, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a cluster snapshot by ID and version.
    /// </summary>
    void Delete(Guid clusterId, Version clusterVersion);

    /// <summary>
    /// Gets metadata for all stored cluster snapshots, optionally filtered by cluster ID.
    /// </summary>
    /// <remarks>
    /// If <paramref name="clusterId"/> is specified, only snapshots for that cluster will be returned.
    /// To ensure <see cref="ClusterInfo.IsActive"/> is accurate, check <see cref="DbContext.IClusterDbContext.ActiveClusters"/>.
    /// </remarks>
    Task<IEnumerable<ClusterInfo>> GetClusterInfos(Guid? clusterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a cluster snapshot exists for the specified ID and version.
    /// </summary>
    bool Exists(Guid clusterId, Version clusterVersion);

    /// <summary>
    /// Checks if the snapshot store is empty.
    /// </summary>
    bool IsEmpty();
}
