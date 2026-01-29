using ClusterManagement.Internal.Contracts;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Designs;

/// <summary>
/// Provides storage operations for cluster dependencies (packages).
/// </summary>
public interface IClusterDependencyStore
{
    /// <summary>
    /// Initializes the cluster dependency store.
    /// </summary>
    Task Initialize(CancellationToken stoppingToken);

    /// <summary>
    /// Loads all cluster dependencies from storage.
    /// </summary>    
    Task<IReadOnlyCollection<ClusterDependency>> LoadDependencies(CancellationToken cancellationToken);

    /// <summary>
    /// Queues cluster dependency operations that will be merged into storage on the next call of <see cref="ProcessQueuedOperations(CancellationToken)"/>.
    /// </summary>
    Task EnqueueOperations(List<ClusterDependencyOperation> operations, CancellationToken cancellationToken);

    /// <summary>
    /// Updates cluster dependencies in storage by merging queued operations with existing dependencies.    
    /// </summary>
    Task ProcessQueuedOperations(CancellationToken cancellationToken);

    /// <summary>
    /// Returns the currently enqueued operations.
    /// </summary>
    Task<IReadOnlyCollection<ClusterDependencyOperation>> GetEnqueuedOperations(CancellationToken cancellationToken);

    /// <summary>
    /// Returns a map of packages and their versions stored in the dependency store.
    /// </summary>
    Dictionary<string, IEnumerable<Version>> BuildStoredPackageVersionMap();
}
