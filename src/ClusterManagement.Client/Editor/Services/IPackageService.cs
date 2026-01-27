using ClusterManagement.Internal.Contracts;

namespace ClusterManagement.Client.Editor.Services;

public interface IPackageService
{
    Task DeletePackage(string name, string version);
    Task<IEnumerable<ClusterPackageInfo>> GetPackages();
    Task UploadPackage(byte[] data);
    Task UpdatePackages(List<ClusterDependencyOperation> operations);
}
