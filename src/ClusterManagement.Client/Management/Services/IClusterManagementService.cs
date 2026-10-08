using ClusterManagement.Client.Management.Models;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Management.Services;

public interface IClusterManagementService
{
    event EventHandler<ClusterUpdateStarted>? OnClusterUpdateStarted;
    event EventHandler<ClusterUpdateChanged>? OnClusterUpdateChanged;
    event EventHandler<ClusterUpdateCompleted>? OnClusterUpdateCompleted;
    event EventHandler<ClusterUpdateFailed>? OnClusterUpdateFailed;
    /// <summary>
    /// Raised when a deployment sent by this service is rejected because another one is still running.
    /// </summary>
    event EventHandler<ClusterUpdateRejected>? OnClusterUpdateRejected;

    Task DeployCluster(Guid clusterId, Version version, CancellationToken cancellationToken = default);

    Task<List<ClusterInfoModel>> QueryClusterVersions(Guid? clusterId = null, CancellationToken cancellationToken = default);

    Task RemoveCluster(Guid clusterId, Version version, CancellationToken cancellationToken = default);

    Task<Cluster> QueryCluster(Guid clusterId, Version version, CancellationToken cancellationToken = default);

    Task ResetSagaState(Guid clusterId, CancellationToken token);

    Task<DeploymentState?> QueryClusterDeploymentState(Guid clusterId, CancellationToken cancellationToken = default);
}
