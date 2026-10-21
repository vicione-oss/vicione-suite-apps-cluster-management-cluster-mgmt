using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend;

internal static partial class ClusterManagementModuleInitializerLogging
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Snapshot of active cluster {ClusterId}-{MissingVersion} is missing. Activated stored version {ClusterVersion} instead.")]
    internal static partial void LogActiveClusterVersionReplaced(this ILogger logger, Guid clusterId, Version missingVersion, Version clusterVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Snapshot of active cluster {ClusterId}-{MissingVersion} is missing and no lower version is stored. Recreated and activated default version {ClusterVersion}.")]
    internal static partial void LogActiveClusterDefaultVersionRecreated(this ILogger logger, Guid clusterId, Version missingVersion, Version clusterVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Snapshot of active cluster {ClusterId}-{MissingVersion} is missing and no version of the cluster is stored. Removed it from the active clusters.")]
    internal static partial void LogActiveClusterRemoved(this ILogger logger, Guid clusterId, Version missingVersion);
}
