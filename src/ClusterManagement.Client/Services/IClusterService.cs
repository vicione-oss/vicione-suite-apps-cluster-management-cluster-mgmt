using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Requests;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Services;

public interface IClusterService
{
    Task<Guid> CommitChange(Cluster cluster, CommitClusterOptions? options, CancellationToken cancellationToken = default);
    Task<Cluster> Query(Guid clusterId, Version? version, CancellationToken cancellationToken = default);
    Task<Version?> QueryLatestVersion(Guid clusterId, CancellationToken cancellationToken = default);
    Task<List<Version>> QueryVersions(Guid clusterId, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, List<Version>>> QueryVersions(CancellationToken cancellationToken = default);
    Task<List<ClusterInfo>> QueryClusterInfos(Guid? clusterId = null, CancellationToken cancellationToken = default);
    // redundant with DeployService
    Task Remove(Guid clusterId, Version version, CancellationToken cancellationToken = default);
}
