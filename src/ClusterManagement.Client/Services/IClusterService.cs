using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Requests;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Services;

public interface IClusterService
{
    Task CommitChange(Cluster cluster, CommitClusterOptions? options);
    Task<Cluster> Query(Guid clusterId, Version? version);
    Task<Version?> QueryLatestVersion(Guid clusterId);
    Task<List<Version>> QueryVersions(Guid clusterId);
    Task<Dictionary<Guid, List<Version>>> QueryVersions();
    Task<List<ClusterInfo>> QueryClusterInfos(Guid? clusterId = null);
    // redundant with DeployService
    Task Remove(Guid clusterId, Version version);
}
