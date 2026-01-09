using ClusterManagement.Client.Management.Models;
using ClusterManagement.Public.Events;

namespace ClusterManagement.Client.Management.Services;

public interface IClusterManagementService
{
    event EventHandler<ClusterUpdateStarted>? OnClusterUpdateStarted;
    event EventHandler<ClusterUpdateChanged>? OnClusterUpdateChanged;
    event EventHandler<ClusterUpdateCompleted>? OnClusterUpdateCompleted;
    event EventHandler<ClusterUpdateFailed>? OnClusterUpdateFailed;

    Task DeployCluster(Guid clusterId, Version version);

    Task<List<ClusterInfoModel>> QueryClusterVersions(Guid? clusterId = null);

    Task RemoveCluster(Guid clusterId, Version version);

    Task DownloadClusterJson(Guid clusterId, Version version);
}
