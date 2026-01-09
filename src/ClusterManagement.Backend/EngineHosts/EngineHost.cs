using ClusterManagement.Backend.Engines;

namespace ClusterManagement.Backend.EngineHosts;

public sealed class EngineHost
{
    public Guid Id { get; init; }
    public bool ElevatedPrivileges { get; set; }
    public List<EngineDeployment> Deployments { get; init; } = [];
}
