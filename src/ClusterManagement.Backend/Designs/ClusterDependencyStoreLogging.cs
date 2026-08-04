using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Designs;

internal static partial class ClusterDependencyStoreLogging
{
    [LoggerMessage(LogLevel.Warning, "Failed to parse version '{Version}' of package '{Name}' from shipped packages file, skipping version comparison")]
    internal static partial void LogShippedParseVersionFailure(this ILogger<ClusterDependencyStore> logger, string version, string name);

    [LoggerMessage(LogLevel.Warning, "Failed to parse version '{Version}' of package '{Name}' from packages file, skipping version comparison")]
    internal static partial void LogParseVersionFailure(this ILogger<ClusterDependencyStore> logger, string version, string name);

    [LoggerMessage(LogLevel.Information, "Initialized cluster packages from embedded resources")]
    internal static partial void LogInitializationFromEmbeddedResources(this ILogger<ClusterDependencyStore> logger);

    [LoggerMessage(LogLevel.Debug, "No package updates to queue")]
    internal static partial void LogNothingToQueue(this ILogger<ClusterDependencyStore> logger);

    [LoggerMessage(LogLevel.Debug, "Removing uninstall operation from queue because '{Name}' version '{Version}' it's not installed yet")]
    internal static partial void LogRemovingUninstallOperation(this ILogger<ClusterDependencyStore> logger, string name, string version);

    [LoggerMessage(LogLevel.Debug, "Removing install operation from queue because '{Name}' version '{Version}' is already installed")]
    internal static partial void LogRemovingInstallOperation(this ILogger<ClusterDependencyStore> logger, string name, string version);

    [LoggerMessage(LogLevel.Information, "No package operations to queue, deleting existing operations file '{FileName}'")]
    internal static partial void LogOperationsFileDeletion(this ILogger<ClusterDependencyStore> logger, string? fileName);

    [LoggerMessage(LogLevel.Information, "Enqueued {Count} package operations to '{FileName}'")]
    internal static partial void LogFileEntriesCount(this ILogger<ClusterDependencyStore> logger, int count, string fileName);

    [LoggerMessage(LogLevel.Debug, "No queued package operations found")]
    internal static partial void LogNoPackagesQueued(this ILogger<ClusterDependencyStore> logger);

    [LoggerMessage(LogLevel.Debug, "Write {Count} packages to '{FileName}'")]
    internal static partial void LogWriteCount(this ILogger<ClusterDependencyStore> logger, int count, string fileName);

    [LoggerMessage(LogLevel.Debug, "Deleting queued package updates file '{FilePath}'")]
    internal static partial void LogUpdateFileDeletion(this ILogger<ClusterDependencyStore> logger, string? filePath);

    [LoggerMessage(LogLevel.Warning, "Failed to deserialize queued package updates from '{FilePath}'")]
    internal static partial void LogDeserializationFailure(this ILogger<ClusterDependencyStore> logger, string? filePath);

    [LoggerMessage(LogLevel.Warning, "Queued package operations file '{FilePath}' is corrupted and gets discarded")]
    internal static partial void LogCorruptedOperationsFile(this ILogger<ClusterDependencyStore> logger, Exception exception, string? filePath);

    [LoggerMessage(LogLevel.Warning, "Packages file '{FilePath}' is corrupted and gets restored from embedded resources")]
    internal static partial void LogCorruptedPackagesFile(this ILogger<ClusterDependencyStore> logger, Exception exception, string? filePath);
}
