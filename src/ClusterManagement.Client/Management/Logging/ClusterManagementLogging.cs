using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Client.Management.Logging;

internal static partial class ClusterManagementLogging
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending DeployCluster clusterId:{ClusterId} version:{Version} -> correlationId:{CorrelationId}")]
    internal static partial void LogDeployCluster(this ILogger logger, Guid clusterId, Version version, Guid? correlationId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Received {Name} event triggered by us. Correlation id: {Id}")]
    internal static partial void LogReceivedOwnEvent(this ILogger logger, string name, Guid? id);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Received {Name} event not triggered by us. Correlation id: {Id}")]
    internal static partial void LogReceivedExternalEvent(this ILogger logger, string name, Guid? id);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to query deployment state for cluster {ClusterId}: {ErrorInfo}")]
    internal static partial void LogQueryClusterDeploymentStateError(this ILogger logger, Guid clusterId, ErrorInfo? errorInfo);

    [LoggerMessage(Level = LogLevel.Error, Message = "{ActionName} failed.")]
    internal static partial void LogFailure(this ILogger logger, Exception exception, string actionName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to download cluster {Name} version '{Version}'")]
    internal static partial void LogDownloadClusterJsonFailure(this ILogger logger, Exception exception, string name, Version version);
}
