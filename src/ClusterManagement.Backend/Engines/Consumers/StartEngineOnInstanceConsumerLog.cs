using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Engines.Consumers;

internal static partial class StartEngineOnInstanceConsumerLog
{
    [LoggerMessage(LogLevel.Debug, "Manually starting engine '{EngineId}' on '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogStartingEngine(this ILogger<StartEngineOnInstanceConsumer> logger, Guid engineId, Guid hostId, Guid deploymentId);
}
