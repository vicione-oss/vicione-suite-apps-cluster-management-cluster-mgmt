using Microsoft.Extensions.Logging;

namespace ClusterManagement.Client.Services;

internal static partial class ClusterServiceLogging
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending mediator query QueryClusterInfos")]
    internal static partial void LogQueryClusterInfos(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "QueryClusterInfos result contains {Count} items")]
    internal static partial void LogQueryClusterInfosResult(this ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Query cluster width id {ClusterId}")]
    internal static partial void LogQueryCluster(this ILogger logger, Guid clusterId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Query cluster returned item id {ClusterId}")]
    internal static partial void LogQueryClusterResult(this ILogger logger, Guid clusterId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Publishing CommitClusterChange width id {ClusterId}, version {ClusterVersion}")]
    internal static partial void LogCommitClusterChange(this ILogger logger, Guid clusterId, Version clusterVersion);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Action} took {Elapsed}")]
    internal static partial void LogElapsedTime(this ILogger logger, string action, TimeSpan elapsed);
}
