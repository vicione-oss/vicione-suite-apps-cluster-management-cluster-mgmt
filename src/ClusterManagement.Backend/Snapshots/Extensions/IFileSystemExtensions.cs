using System.IO.Abstractions;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Snapshots.Extensions;

internal static class IFileSystemExtensions
{
    private const string SnapshotsDirectory = "Snapshots";

    public static string GetSnapshotsPath(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws)
        => fileSystem.Path.Combine(ws.Home, SnapshotsDirectory);
}
