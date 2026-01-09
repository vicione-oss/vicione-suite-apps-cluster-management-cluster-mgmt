namespace ClusterManagement.Backend.Engines;

public sealed class EngineDeployment
{
    public Guid Id { get; init; }
    public Guid EngineHostId { get; init; }
    public Guid EngineId { get; init; }
    public bool ShouldRun { get; set; }
    public string Version { get; set; } = string.Empty;
    public int Replica { get; set; }
    public int Checksum { get; set; }
}
