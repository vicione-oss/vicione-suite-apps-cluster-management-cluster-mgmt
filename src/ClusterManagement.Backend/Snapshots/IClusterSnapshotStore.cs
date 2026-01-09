using System.Text.Json.Nodes;
using ClusterManagement.Public.Requests;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Snapshots;

public interface IClusterSnapshotStore
{
    Task Store(byte[] compressedClusterJson, ClusterSnapshotInfo info, CancellationToken cancellationToken = default);

    Task Store<T>(Guid clusterId, Version clusterVersion, JsonObject info, T value, Func<Stream, T, CancellationToken, Task> write, bool replace, CancellationToken cancellationToken);

    Task Store(Cluster cluster, CancellationToken cancellationToken);

    Task<Cluster?> Load(Guid clusterId, Version clusterVersion, CancellationToken cancellationToken = default);

    Task<byte[]> LoadCompressedJson(Guid clusterId, Version clusterVersion, CancellationToken cancellationToken = default);

    Task<T?> Load<T>(Guid clusterId, Version clusterVersion, Func<Stream, CancellationToken, Task<T>> read, CancellationToken cancellationToken = default);

    void Delete(Guid clusterId, Version clusterVersion);

    Task<IEnumerable<ClusterInfo>> GetClusterInfos(Guid? clusterId, CancellationToken cancellationToken = default);

    bool Exists(Guid clusterId, Version clusterVersion);

    bool IsEmpty();
}
