using System.IO.Abstractions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Iodds.Extensions;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Iodds;

/// <summary>
/// Removes uploaded IODD archives whose <see cref="Public.Iodds.Commands.AddIodd"/> was never consumed (e.g. circuit gone, backend down).
/// </summary>
internal sealed partial class IoddUploadCleanup(
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IFileSystem fileSystem,
    ILogger<IoddUploadCleanup> logger)
{
    // Recent archives are kept, the client may still be writing or about to send them.
    public void CleanupLeftovers(TimeSpan uploadedIoddMaxAge)
        => fileSystem.DeleteFilesOlderThan(
            fileSystem.GetUploadedIoddsPath(workspaceProvider),
            uploadedIoddMaxAge,
            path => LogUploadedIoddDeletion(logger, path),
            (ex, path) => LogUploadedIoddDeleteFailure(logger, ex, path));

    [LoggerMessage(LogLevel.Information, "Deleting stale uploaded IODD archive '{Path}'")]
    private static partial void LogUploadedIoddDeletion(ILogger<IoddUploadCleanup> logger, string path);

    [LoggerMessage(LogLevel.Warning, "Failed to delete stale uploaded IODD archive '{Path}'")]
    private static partial void LogUploadedIoddDeleteFailure(ILogger<IoddUploadCleanup> logger, Exception exception, string path);
}
