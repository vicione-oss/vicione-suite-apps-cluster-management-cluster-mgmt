using Microsoft.Extensions.Logging;

namespace ClusterManagement.Client.Editor.Services;

internal static partial class ServiceLogging
{
    [LoggerMessage(Level = LogLevel.Error, Message = "{ActionName} failed.")]
    internal static partial void LogFailure(this ILogger logger, Exception exception, string actionName);
}
