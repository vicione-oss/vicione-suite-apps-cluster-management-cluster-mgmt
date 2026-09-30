using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Designs;

/// <summary>
/// Provides functionality for storing and managing cluster packages in the file system.
/// </summary>
public interface IPackageStore
{
    /// <summary>
    /// Stores a cluster package from a ZIP archive and queues it for installation.
    /// </summary>    
    Task<ClusterDependency> StorePackage(string filePath, bool overwrite = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a cluster package from the file system and queues it for uninstallation.
    /// </summary>   
    Task DeletePackage(ClusterDependency dependency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes what an interrupted upload left behind: replaced and staged package versions, and uploaded archives
    /// older than <paramref name="uploadedPackageMaxAge"/>. Failures are logged only.
    /// </summary>
    void CleanupLeftovers(TimeSpan uploadedPackageMaxAge);
}
