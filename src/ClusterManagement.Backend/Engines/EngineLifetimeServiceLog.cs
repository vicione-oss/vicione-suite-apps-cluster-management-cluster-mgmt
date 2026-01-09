using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Engines;

internal static partial class EngineLifetimeServiceServiceLog
{
    [LoggerMessage(LogLevel.Error, "Failed to execute downloads")]
    internal static partial void DownloadsFailed(this ILogger<EngineLifetimeService> logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Failed to start engines automatically")]
    internal static partial void StartingEnginesFailed(this ILogger<EngineLifetimeService> logger, Exception exception);

    [LoggerMessage(LogLevel.Debug, "The file '{FileName}' keeps engines running.")]
    internal static partial void KeepEnginesRunning(this ILogger<EngineLifetimeService> logger, string fileName);
}
