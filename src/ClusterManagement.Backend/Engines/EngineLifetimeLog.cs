using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Engines;

internal static partial class EngineLifetimeLog
{
    [LoggerMessage(LogLevel.Debug, "Auto starting engine host '{HostId}'")]
    internal static partial void LogStartingEngineHost(this ILogger<EngineLifetime> logger, Guid hostId);

    [LoggerMessage(LogLevel.Error, "Failed to auto start engine host '{HostId}'")]
    internal static partial void LogStartingEngineHostFailed(this ILogger<EngineLifetime> logger, Exception ex, Guid hostId);

    [LoggerMessage(LogLevel.Debug, "Auto starting engine '{EngineId}' on engine host '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogStartingEngine(this ILogger<EngineLifetime> logger, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(LogLevel.Error, "Failed to auto start engine '{EngineId}' on engine host '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogStartingEngineFailed(this ILogger<EngineLifetime> logger, Exception ex, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(LogLevel.Debug, "Auto stopping engine host '{HostId}'")]
    internal static partial void LogStoppingEngineHost(this ILogger<EngineLifetime> logger, Guid hostId);

    [LoggerMessage(LogLevel.Error, "Failed to auto stop engine host '{HostId}'")]
    internal static partial void LogStoppingEngineHostFailed(this ILogger<EngineLifetime> logger, Exception ex, Guid hostId);

    [LoggerMessage(LogLevel.Debug, "Auto stopping engine '{EngineId}' on engine host '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogStoppingEngine(this ILogger<EngineLifetime> logger, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(LogLevel.Error, "Failed to auto stop engine '{EngineId}' on engine host '{HostId}' as deployment '{DeploymentId}'")]
    internal static partial void LogStoppingEngineFailed(this ILogger<EngineLifetime> logger, Exception ex, Guid engineId, Guid hostId, Guid deploymentId);

    [LoggerMessage(LogLevel.Debug, "Remove existing saga state for cluster '{ClusterId}'")]
    internal static partial void LogRemoveDeploySagaState(this ILogger<EngineLifetime> logger, Guid clusterId);

    [LoggerMessage(LogLevel.Error, "Failed to recover the deploy saga state on startup")]
    internal static partial void LogRecoverDeploySagaFailed(this ILogger<EngineLifetime> logger, Exception ex);
}
