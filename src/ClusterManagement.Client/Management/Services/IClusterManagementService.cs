using ClusterManagement.Client.Management.Models;
using ClusterManagement.Internal.Events;
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
    event EventHandler<ClusterNodeDeploymentStepChanged>? OnClusterNodeDeploymentStepChanged;
    event EventHandler<ClusterNodeDeploymentPlanned>? OnClusterNodeDeploymentPlanned;

    /// <summary>
    /// Raised with the outcome of the last reset this service sent.
    /// </summary>
    event EventHandler<DeploymentStateReset>? OnDeploymentStateReset;

    Task DeployCluster(Guid clusterId, Version version, CancellationToken cancellationToken = default);

    Task<List<ClusterInfoModel>> QueryClusterVersions(Guid? clusterId = null, CancellationToken cancellationToken = default);

    Task RemoveCluster(Guid clusterId, Version version, CancellationToken cancellationToken = default);

    Task<Cluster> QueryCluster(Guid clusterId, Version version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every deployment state of the cluster; <see cref="OnDeploymentStateReset"/> tells what it removed.
    /// </summary>
    Task ResetSagaState(Guid clusterId, string requestedBy, CancellationToken token);

    Task<DeploymentState?> QueryClusterDeploymentState(Guid clusterId, CancellationToken cancellationToken = default);
}
