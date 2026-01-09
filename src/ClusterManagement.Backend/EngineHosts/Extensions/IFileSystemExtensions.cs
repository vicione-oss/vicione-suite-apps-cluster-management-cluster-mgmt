using System.IO.Abstractions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Internal;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.EngineHosts.Extensions;

internal static class IFileSystemExtensions
{
    private const string EngineHostVersionsDirectory = "EngineHost";
    private const string DeploymentsDirectory = "Deployments";

    public static string GetEngineHostVersionsPath(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws)
        => fileSystem.Path.Combine(ws.Cache, EngineHostVersionsDirectory);

    public static string GetDeploymentsPath(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws)
        => fileSystem.Path.Combine(ws.Cache, DeploymentsDirectory);

    internal static string GetEngineHostInstallDirectory(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider, ClusterManagementOptions options)
    {
        var packageName = options.GetEngineHostPackageName();
        var engineHostVersionsDirectory = fileSystem.GetEngineHostVersionsPath(workspaceProvider);
        return fileSystem.Path.Combine(engineHostVersionsDirectory, packageName);
    }
}
