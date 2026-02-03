using ClusterManagement.Internal.Contracts;

namespace ClusterManagement.Client.Editor.Services;

public interface IPackageService
{
    string MinCoreVersion { get; }

    Task DeletePackage(string name, string version, CancellationToken cancellationToken = default);
    Task<IEnumerable<ClusterPackageInfo>> GetPackages(List<string>? names = null, CancellationToken cancellationToken = default);
    Task UploadPackage(byte[] data, CancellationToken cancellationToken = default);
    Task UpdatePackages(List<ClusterDependencyOperation> operations, CancellationToken cancellationToken = default);
}
