using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Consumers;

internal static partial class RequestHandlingFailedLogging
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to handle {Request}")]
    internal static partial void LogHandleException(this ILogger logger, Exception exception, string request);
}
