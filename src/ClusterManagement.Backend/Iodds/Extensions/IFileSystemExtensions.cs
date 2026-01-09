using System.IO.Abstractions;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Iodds.Extensions;

internal static class IFileSystemExtensions
{
    private const string IoddsDirectory = "Iodds";

    public static string GetIoddsPath(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws)
        => fileSystem.Path.Combine(ws.Home, IoddsDirectory);
}
