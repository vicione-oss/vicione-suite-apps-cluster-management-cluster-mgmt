using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Engines.Consumers;

internal static partial class TearDownEngineOnInstanceConsumerLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Manually tearing down engine '{EngineId}' on '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogTearingDownEngine(this ILogger<TearDownEngineOnInstanceConsumer> logger, Guid engineId, Guid hostId, Guid deploymentId);
}
