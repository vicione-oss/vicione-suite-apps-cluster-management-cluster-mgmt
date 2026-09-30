using System.IO.Abstractions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Internal.Contracts;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Iodds.Extensions;

internal static class IFileSystemExtensions
{
    private const string IoddsDirectory = "Iodds";

    public static string GetIoddsPath(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws)
        => fileSystem.Path.Combine(ws.Home, IoddsDirectory);

    /// <summary>
    /// Gets the directory the client's stream upload handler stores uploaded IODD archives in.
    /// </summary>
    public static string GetUploadedIoddsPath(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws)
        => fileSystem.Path.Combine(ws.Cache, IoddUpload.DirectoryName);

    /// <summary>
    /// Returns whether <paramref name="filePath"/> names a file directly inside the upload directory of IODD archives.
    /// </summary>
    public static bool IsUploadedIoddFile(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws, string filePath)
        => fileSystem.IsFileDirectlyInDirectory(fileSystem.GetUploadedIoddsPath(ws), filePath);
}
