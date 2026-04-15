using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Snapshots;

internal static partial class ClusterSnapshotStoreLogging
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Cluster '{FileName}' could not be deleted. Cluster not found.")]
    internal static partial void LogClusterNotDeleted(this ILogger logger, string fileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get information for cluster {ClusterId}")]
    internal static partial void LogGetClusterInfosError(this ILogger logger, Exception exception, Guid? clusterId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cluster {ClusterId}-{ClusterVersion} could not be loaded. Cluster not found.")]
    internal static partial void LogClusterNotLoaded(this ILogger logger, Guid clusterId, Version clusterVersion);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load cluster {ClusterId}-{ClusterVersion}")]
    internal static partial void LogLoadClusterError(this ILogger logger, Exception exception, Guid clusterId, Version clusterVersion);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to store cluster {ClusterId}-{ClusterVersion}")]
    internal static partial void LogStoreClusterError(this ILogger logger, Exception exception, Guid clusterId, Version clusterVersion);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Migrate cluster if needed {ClusterId}-{ClusterVersion}")]
    internal static partial void LogMigrationStart(this ILogger logger, Guid clusterId, Version clusterVersion);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Migrate cluster {ClusterId}-{ClusterVersion} took {Elapsed} ms.")]
    internal static partial void LogMigrationEnd(this ILogger logger, Guid clusterId, Version clusterVersion, double elapsed);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to migrate cluster snapshots.")]
    internal static partial void LogMigrationError(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replaced dependency '{Name}' with version {Version} in cluster {ClusterId}-{ClusterVersion}.")]
    internal static partial void LogDependencyReplaced(this ILogger logger, string name, string version, Guid clusterId, Version clusterVersion);
}
