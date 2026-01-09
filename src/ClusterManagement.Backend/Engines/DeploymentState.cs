namespace ClusterManagement.Backend.Engines;

public sealed record class DeploymentState(Guid DeploymentIdentifier, EngineState EngineState);
