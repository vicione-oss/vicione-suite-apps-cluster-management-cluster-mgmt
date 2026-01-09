using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetEngineLogResponse : IResponse
{
    public IReadOnlyCollection<LogEntry> LogEntries { get; init; }
    public ErrorInfo? RequestError { get; init; }

    [JsonConstructor]
    public GetEngineLogResponse(IReadOnlyCollection<LogEntry> logEntries) => LogEntries = logEntries;

    public GetEngineLogResponse(ErrorInfo errorInfo)
        : this([])
        => RequestError = errorInfo;
}

public sealed record LogEntry
{
    public LogLevel Type { get; init; }
    public DateTime Timestamp { get; init; }
    public string Text { get; init; } = string.Empty;
}
