using System.IO.Abstractions;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Engines.Extensions;

internal static class IFileSystemExtensions
{
    public static string GetEnginesDirectory(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws)
        => fileSystem.Path.Combine(ws.Cache, "Engines");
}
