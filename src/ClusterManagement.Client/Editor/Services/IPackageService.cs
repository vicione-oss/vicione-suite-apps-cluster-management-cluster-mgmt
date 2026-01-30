using ClusterManagement.Internal.Contracts;

namespace ClusterManagement.Client.Editor.Services;

public interface IPackageService
{
    string MinCoreVersion { get; }

    Task DeletePackage(string name, string version);
    Task<IEnumerable<ClusterPackageInfo>> GetPackages(List<string>? names = null);
    Task UploadPackage(byte[] data);
    Task UpdatePackages(List<ClusterDependencyOperation> operations);
}
