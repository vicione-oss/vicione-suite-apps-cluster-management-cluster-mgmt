using ClusterManagement.Client.Editor.Models;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Services;

public interface IClusterService
{
    /// <summary>
    /// Raised when a cluster change has been successfully committed.
    /// </summary>
    event Func<ClusterChanged, CancellationToken, Task>? ClusterChanged;

    /// <summary>
    /// Raised when a cluster change commit operation has failed.
    /// </summary>
    event Func<CommitClusterChangeFailed, CancellationToken, Task>? CommitClusterChangeFailed;

    /// <summary>
    /// Serializes and commits a change to the specified <paramref name="cluster"/>, 
    /// then waits for the operation to complete.
    /// </summary>
    /// <param name="cluster">The cluster to commit.</param>
    /// <param name="options">
    /// Optional commit options, such as whether to increment the version or clean up dependencies.
    /// </param>
    Task<IClusterServiceResult> CommitChange(Cluster cluster, CommitClusterOptions? options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries a cluster by its ID and an optional version.
    /// </summary>    
    Task<Cluster> Query(Guid clusterId, Version? version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the latest available version of the specified cluster.
    /// </summary>    
    Task<Version?> QueryLatestVersion(Guid clusterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries all available versions of the specified cluster.
    /// </summary>    
    Task<List<Version>> QueryVersions(Guid clusterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries all available versions for all clusters, grouped by cluster ID.
    /// </summary>    
    Task<Dictionary<Guid, List<Version>>> QueryVersions(CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries cluster metadata, optionally filtered by cluster ID.
    /// </summary>    
    Task<List<ClusterInfo>> QueryClusterInfos(Guid? clusterId = null, CancellationToken cancellationToken = default);

    // redundant with DeployService
    /// <summary>
    /// Removes the specified version of a cluster.
    /// </summary>
    /// <param name="clusterId">The unique identifier of the cluster to remove.</param>
    /// <param name="version">The specific version of the cluster to remove.</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that returns an <see cref="IClusterServiceResult"/> indicating whether the operation succeeded or failed.
    /// </returns>
    Task<IClusterServiceResult> Remove(Guid clusterId, Version version, CancellationToken cancellationToken = default);
}
