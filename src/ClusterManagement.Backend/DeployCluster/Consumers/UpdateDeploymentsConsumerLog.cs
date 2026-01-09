using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.DeployCluster.Consumers;

internal static partial class UpdateDeploymentsConsumerLog
{
    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Deploying engine host '{HostId}' ({HostName})")]
    internal static partial void LogDeployingEngineHost(this ILogger<UpdateDeploymentsConsumer> logger, Guid hostId, string hostName);

    [LoggerMessage(EventId = 12, Level = LogLevel.Debug, Message = "Starting engine host '{HostId}'")]
    internal static partial void LogStartingEngineHost(this ILogger<UpdateDeploymentsConsumer> logger, Guid hostId);

    [LoggerMessage(EventId = 13, Level = LogLevel.Debug, Message = "Stopping engine host '{HostId}'")]
    internal static partial void LogStoppingEngineHost(this ILogger<UpdateDeploymentsConsumer> logger, Guid hostId);

    [LoggerMessage(EventId = 15, Level = LogLevel.Debug, Message = "Tearing down engine host '{HostId}'")]
    internal static partial void LogTearingDownEngineHost(this ILogger<UpdateDeploymentsConsumer> logger, Guid hostId);

    [LoggerMessage(EventId = 17, Level = LogLevel.Debug, Message = "Updating app settings of engine host '{HostId}'")]
    internal static partial void LogUpdatingEngineHost(this ILogger<UpdateDeploymentsConsumer> logger, Guid hostId);

    [LoggerMessage(EventId = 18, Level = LogLevel.Debug, Message = "App settings of engine host '{HostId}' are up-to-date.")]
    internal static partial void LogSkipEngineHostUpdate(this ILogger<UpdateDeploymentsConsumer> logger, Guid hostId);


    [LoggerMessage(EventId = 21, Level = LogLevel.Debug, Message = "Deploying engine '{EngineId}' ({EngineName}) on '{HostId}' ({HostName})")]
    internal static partial void LogDeployingEngine(this ILogger<UpdateDeploymentsConsumer> logger, Guid engineId, string engineName, Guid hostId, string hostName);

    [LoggerMessage(EventId = 22, Level = LogLevel.Debug, Message = "Starting engine '{EngineId}' on '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogStartingEngine(this ILogger<UpdateDeploymentsConsumer> logger, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(EventId = 23, Level = LogLevel.Debug, Message = "Stopping engine '{EngineId}' on '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogStoppingEngine(this ILogger<UpdateDeploymentsConsumer> logger, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(EventId = 25, Level = LogLevel.Debug, Message = "Tearing down engine '{EngineId}' on '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogTearingDownEngine(this ILogger<UpdateDeploymentsConsumer> logger, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(EventId = 27, Level = LogLevel.Debug, Message = "Engine '{EngineId}' on '{HostId}' as deployment '{DeploymentId}' needs update.")]
    internal static partial void LogUpdatingEngine(this ILogger<UpdateDeploymentsConsumer> logger, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(EventId = 28, Level = LogLevel.Debug, Message = "Engine '{EngineId}' on '{HostId}' as deployment '{DeploymentId}' is up-to-date.")]
    internal static partial void LogSkipEngineUpdate(this ILogger<UpdateDeploymentsConsumer> logger, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(EventId = 29, Level = LogLevel.Debug, Message = "Engine '{EngineId}' on '{HostId}' as deployment '{DeploymentId}' is missing.")]
    internal static partial void LogRestoreMissingEngine(this ILogger<UpdateDeploymentsConsumer> logger, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(EventId = 30, Level = LogLevel.Debug, Message = "Deployment '{DeploymentId}' on '{HostId}' is unexpected.")]
    internal static partial void LogRemoveUnexpectedEngine(this ILogger<UpdateDeploymentsConsumer> logger, Guid hostId, Guid deploymentId);
}
