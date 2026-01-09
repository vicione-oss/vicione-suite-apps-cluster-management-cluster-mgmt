using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Engines;

public sealed class Engine
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint CycleTime { get; set; }
    public int ChainIndex { get; set; }
    public LogLevel LogLevel { get; set; }
}
