using System.IO.Abstractions;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Designs.Extensions;

internal static class IFileSystemExtensions
{
    private const string DependenciesDirectory = "Dependencies";

    public static string GetDependenciesPath(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws)
        => fileSystem.Path.Combine(ws.Cache, DependenciesDirectory);
}
