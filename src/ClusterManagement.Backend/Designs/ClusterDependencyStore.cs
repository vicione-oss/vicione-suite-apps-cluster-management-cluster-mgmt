using System.IO.Abstractions;
using ClusterManagement.Internal;
using Microsoft.Extensions.Options;
using Sdk.Backend.Modules;
using Sdk.Messaging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Designs;

internal sealed class ClusterDependencyStore(
    IFileSystem fileSystem,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IOptions<ClusterManagementOptions> options) : IClusterDependencyStore
{
    public async Task<IReadOnlyCollection<ClusterDependency>> LoadDependencies(CancellationToken cancellationToken)
    {
        var packagesFilename = PackagesFileProvider.GetPackagesFilepath(workspaceProvider.Home, options.Value.PackageSet, fileSystem);
        return await PackagesFileProvider.DeserializePackagesFile(packagesFilename, DefaultJsonSerializerSettings.Default, cancellationToken);
    }
}
