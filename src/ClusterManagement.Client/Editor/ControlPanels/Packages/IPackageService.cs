using ClusterManagement.Client.Editor.ControlPanels.Packages.Models;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages;

/// <summary>
/// Provides operations for managing cluster packages, including installing, updating,
/// uploading, and deleting packages, as well as receiving package change notifications.
/// </summary>
public interface IPackageService
{
    /// <summary>
    /// Gets the minimum required core version for the currently loaded packages.
    /// </summary>
    string MinCoreVersion { get; }

    /// <summary>
    /// Raised when a cluster package has changed (e.g. installed or deleted).
    /// </summary>
    event Func<ClusterPackageChanged, CancellationToken, Task>? PackageChanged;

    /// <summary>
    /// Raised when cluster dependencies have changed.
    /// </summary>
    event Func<ClusterDependenciesChanged, CancellationToken, Task>? DependenciesChanged;

    /// <summary>
    /// Deletes a package with the specified name and version from the cluster.
    /// </summary>    
    Task<IPackageServiceResult> DeletePackage(string name, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves available cluster packages, optionally filtered by name.
    /// </summary>    
    Task<IEnumerable<ClusterPackageInfo>> GetPackages(List<string>? names = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a package to the cluster from an uploaded archive file.
    /// </summary>
    /// <param name="filename">Path of the uploaded package archive; the backend removes it once it has been processed.</param>
    Task<IPackageServiceResult> UploadPackage(string filename, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a set of dependency operations to update the cluster's package dependencies.
    /// </summary>    
    Task<IPackageServiceResult> UpdatePackages(List<ClusterDependencyOperation> operations, CancellationToken cancellationToken = default);
}
