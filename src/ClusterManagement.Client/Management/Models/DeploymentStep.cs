namespace ClusterManagement.Client.Management.Models;

public enum DeploymentStepState
{
    Pending,
    Current,
    Done,
    Failed,
}

public sealed record DeploymentStep(DeploymentPhase Phase, DeploymentStepState State);
