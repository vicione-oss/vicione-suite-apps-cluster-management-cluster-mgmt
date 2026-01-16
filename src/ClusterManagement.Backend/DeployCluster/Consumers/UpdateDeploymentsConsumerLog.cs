using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.DeployCluster.Consumers;

internal static partial class UpdateDeploymentsConsumerLog
{
    /* General */

    [LoggerMessage(LogLevel.Information, "Deploying {engineHosts} engine hosts, {engines} engines with {functionBlocks} FunctionBlocks, and {dataPorts} data ports...")]
    internal static partial void LogDeploymentVolume(this ILogger<UpdateDeploymentsConsumer> logger, int engineHosts, int engines, int functionBlocks, int dataPorts);

    /* Engine Host */

    [LoggerMessage(LogLevel.Debug, "Deploying engine host '{HostId}' ({HostName})")]
    internal static partial void LogDeployingEngineHost(this ILogger<UpdateDeploymentsConsumer> logger, Guid hostId, string hostName);

    [LoggerMessage(LogLevel.Debug, "Starting engine host '{HostId}'")]
    internal static partial void LogStartingEngineHost(this ILogger<UpdateDeploymentsConsumer> logger, Guid hostId);

    [LoggerMessage(LogLevel.Debug, "Stopping engine host '{HostId}'")]
    internal static partial void LogStoppingEngineHost(this ILogger<UpdateDeploymentsConsumer> logger, Guid hostId);

    [LoggerMessage(LogLevel.Debug, "Tearing down engine host '{HostId}'")]
    internal static partial void LogTearingDownEngineHost(this ILogger<UpdateDeploymentsConsumer> logger, Guid hostId);

    [LoggerMessage(LogLevel.Debug, "Updating app settings of engine host '{HostId}'")]
    internal static partial void LogUpdatingEngineHost(this ILogger<UpdateDeploymentsConsumer> logger, Guid hostId);

    [LoggerMessage(LogLevel.Debug, "App settings of engine host '{HostId}' are up-to-date.")]
    internal static partial void LogSkipEngineHostUpdate(this ILogger<UpdateDeploymentsConsumer> logger, Guid hostId);

    /* Engine */

    [LoggerMessage(LogLevel.Debug, "Deploying engine '{EngineId}' ({EngineName}) on '{HostId}' ({HostName})")]
    internal static partial void LogDeployingEngine(this ILogger<UpdateDeploymentsConsumer> logger, Guid engineId, string engineName, Guid hostId, string hostName);

    [LoggerMessage(LogLevel.Debug, "Starting engine '{EngineId}' on '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogStartingEngine(this ILogger<UpdateDeploymentsConsumer> logger, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(LogLevel.Debug, "Stopping engine '{EngineId}' on '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogStoppingEngine(this ILogger<UpdateDeploymentsConsumer> logger, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(LogLevel.Debug, "Tearing down engine '{EngineId}' on '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogTearingDownEngine(this ILogger<UpdateDeploymentsConsumer> logger, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(LogLevel.Debug, "Engine '{EngineId}' on '{HostId}' as deployment '{DeploymentId}' needs update.")]
    internal static partial void LogUpdatingEngine(this ILogger<UpdateDeploymentsConsumer> logger, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(LogLevel.Debug, "Engine '{EngineId}' on '{HostId}' as deployment '{DeploymentId}' is up-to-date.")]
    internal static partial void LogSkipEngineUpdate(this ILogger<UpdateDeploymentsConsumer> logger, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(LogLevel.Debug, "Engine '{EngineId}' on '{HostId}' as deployment '{DeploymentId}' is missing.")]
    internal static partial void LogRestoreMissingEngine(this ILogger<UpdateDeploymentsConsumer> logger, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(LogLevel.Debug, "Deployment '{DeploymentId}' on '{HostId}' is unexpected.")]
    internal static partial void LogRemoveUnexpectedEngine(this ILogger<UpdateDeploymentsConsumer> logger, Guid hostId, Guid deploymentId);
}
