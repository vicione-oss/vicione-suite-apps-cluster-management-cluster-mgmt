using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Consumers;

internal static partial class CommitClusterChangeConsumerLogging
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Consume {Command} CorrelationId:{CorrelationId} Id:{Id} Version:{Version}")]
    public static partial void LogConsumeCommand(
        this ILogger logger,
        string command,
        Guid? correlationId,
        Guid id,
        Version version);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Set cluster {Id} version to v{Version}")]
    public static partial void LogSetClusterVersion(
        this ILogger logger,
        Guid id,
        Version version);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Increased cluster {Id} version to v{Version}")]
    public static partial void LogIncreasedClusterVersion(
        this ILogger logger,
        Guid id,
        Version version);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "{Command} faulted with errors: {Errors}")]
    public static partial void LogCommandFaulted(
        this ILogger logger,
        string command,
        string errors);
}
