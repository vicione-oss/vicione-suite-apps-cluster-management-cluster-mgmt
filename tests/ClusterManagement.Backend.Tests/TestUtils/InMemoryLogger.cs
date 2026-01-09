using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Tests.TestUtils;

[ProviderAlias("InMemory")]
internal sealed class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, InMemoryLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<InMemoryLogEntry> _logEntries = [];
    private string? _lastMessage;

    internal ConcurrentQueue<InMemoryLogEntry> LogEntries => _logEntries;
    internal string? LastMessage => _lastMessage;

    public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, new InMemoryLogger(Log));
    private void Log(InMemoryLogEntry logEntry, string detailedMessage)
    {
        _logEntries.Enqueue(logEntry);
        _lastMessage = detailedMessage;
    }
    public void Clear() => _logEntries.Clear();
    public void Dispose() => _loggers.Clear();
}

internal sealed class InMemoryLogger(Action<InMemoryLogEntry, string> addLogEntry) : InMemoryLoggerBase, ILogger
{
    internal override void AddNewLogEntry(InMemoryLogEntry logEntry, string detailedMessage) => addLogEntry(logEntry, detailedMessage);
}

internal sealed class InMemoryLogger<T> : InMemoryLoggerBase, ILogger<T>
{
    private readonly ConcurrentQueue<InMemoryLogEntry> _logEntries = [];
    private string? _lastMessage;

    internal ConcurrentQueue<InMemoryLogEntry> LogEntries => _logEntries;
    internal string? LastMessage => _lastMessage;

    internal override void AddNewLogEntry(InMemoryLogEntry logEntry, string detailedMessage)
    {
        _logEntries.Enqueue(logEntry);
        _lastMessage = detailedMessage;
    }

    public void Clear() => _logEntries.Clear();
}

internal abstract class InMemoryLoggerBase : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);

        InMemoryLogEntry logEntry = new(logLevel, eventId, message, exception);

        string detailedMessage;
        if (exception is not null)
        {
            StringBuilder builder = new();
            builder.AppendLine(message);
            builder.Append(exception.ToString());
            detailedMessage = builder.ToString();
        }
        else
        {
            detailedMessage = message;
        }
        AddNewLogEntry(logEntry, detailedMessage);
    }

    internal abstract void AddNewLogEntry(InMemoryLogEntry logEntry, string detailedMessage);
}

internal sealed record class InMemoryLogEntry(LogLevel LogLevel, EventId EventId, string Message, Exception? Exception);
