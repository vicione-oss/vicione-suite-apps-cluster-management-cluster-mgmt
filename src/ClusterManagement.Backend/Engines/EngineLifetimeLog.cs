using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Engines;

internal static partial class EngineLifetimeLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Auto starting engine host '{HostId}'")]
    internal static partial void LogStartingEngineHost(this ILogger<EngineLifetime> logger, Guid hostId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to auto start engine host '{HostId}'")]
    internal static partial void LogStartingEngineHostFailed(this ILogger<EngineLifetime> logger, Exception ex, Guid hostId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Auto starting engine '{EngineId}' on engine host '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogStartingEngine(this ILogger<EngineLifetime> logger, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Failed to auto start engine '{EngineId}' on engine host '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogStartingEngineFailed(this ILogger<EngineLifetime> logger, Exception ex, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Auto stopping engine host '{HostId}'")]
    internal static partial void LogStoppingEngineHost(this ILogger<EngineLifetime> logger, Guid hostId);

    [LoggerMessage(EventId = 12, Level = LogLevel.Error, Message = "Failed to auto stop engine host '{HostId}'")]
    internal static partial void LogStoppingEngineHostFailed(this ILogger<EngineLifetime> logger, Exception ex, Guid hostId);

    [LoggerMessage(EventId = 13, Level = LogLevel.Debug, Message = "Auto stopping engine '{EngineId}' on engine host '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogStoppingEngine(this ILogger<EngineLifetime> logger, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(EventId = 14, Level = LogLevel.Error, Message = "Failed to auto stop engine '{EngineId}' on engine host '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogStoppingEngineFailed(this ILogger<EngineLifetime> logger, Exception ex, Guid engineId, Guid hostId, Guid deploymentId);
}
