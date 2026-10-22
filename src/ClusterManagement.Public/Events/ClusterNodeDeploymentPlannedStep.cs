using ClusterManagement.Public.Enums;

namespace ClusterManagement.Public.Events;

/// <summary>
/// A step of <see cref="ClusterNodeDeploymentPlanned"/>; its ids and names follow the rules of
/// <see cref="ClusterNodeDeploymentStepChanged"/>.
/// </summary>
public sealed record ClusterNodeDeploymentPlannedStep(ClusterNodeDeploymentStep Step)
{
    public Guid? EngineHostId { get; init; }
    public string? EngineHostName { get; init; }
    public Guid? EngineId { get; init; }
    public string? EngineName { get; init; }
}
