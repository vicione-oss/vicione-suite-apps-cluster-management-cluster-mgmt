using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed class EngineLogConfiguration
{
    public CommunicationConfiguration Communication { get; set; } = new();
    public LogLevel LogLevel { get; set; }
}
