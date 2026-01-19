using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Engines.Consumers;

internal static partial class StopEngineOnInstanceConsumerLog
{
    [LoggerMessage(LogLevel.Debug, "Manually stopping engine '{EngineId}' on '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogStoppingEngine(this ILogger<StopEngineOnInstanceConsumer> logger, Guid engineId, Guid hostId, Guid deploymentId);
}
