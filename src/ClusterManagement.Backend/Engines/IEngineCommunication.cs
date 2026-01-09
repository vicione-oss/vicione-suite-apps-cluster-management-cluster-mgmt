using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Engines;

public interface IEngineCommunication
{
    /// <exception cref="InvalidOperationException">The log level cannot be changed by the engine.</exception>
    Task ChangeLogLevel(Guid engineId, LogLevel logLevel, CancellationToken cancellationToken);
    /// <exception cref="InvalidOperationException">The settings cannot be loaded by the engine.</exception>
    Task LoadSettings(Guid engineId, DateTime timestamp, IReadOnlyCollection<PersistenceEntry> settings,
        CancellationToken cancellationToken);
    /// <exception cref="InvalidOperationException">The persistent variables cannot be loaded by the engine.</exception>
    Task LoadPersistentVariables(Guid engineId, DateTime timestamp, IReadOnlyCollection<PersistenceEntry> variables,
        CancellationToken cancellationToken);
}
