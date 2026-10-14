using System.IO.Abstractions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Internal.Contracts;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Connections.Extensions;

internal static class IFileSystemExtensions
{
    /// <summary>
    /// Gets the directory the client's stream upload handler stores uploaded OPC UA certificates in.
    /// </summary>
    public static string GetUploadedOpcUaCertificatesPath(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws)
        => fileSystem.Path.Combine(ws.Cache, OpcUaCertificateUpload.DirectoryName);

    /// <summary>
    /// Returns whether <paramref name="filePath"/> names a file directly inside the upload directory of OPC UA certificates.
    /// </summary>
    public static bool IsUploadedOpcUaCertificateFile(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws, string filePath)
        => fileSystem.IsFileDirectlyInDirectory(fileSystem.GetUploadedOpcUaCertificatesPath(ws), filePath);
}
