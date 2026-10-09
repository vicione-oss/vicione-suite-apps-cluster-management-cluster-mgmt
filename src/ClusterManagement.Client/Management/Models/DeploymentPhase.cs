namespace ClusterManagement.Client.Management.Models;

public enum DeploymentPhase
{
    Starting,
    Committing,
    AddingNodes,
    UpdatingDeployments,
    CleaningUp,
    RollingBack,
    Completed,
    Failed,
}

internal static class DeploymentPhases
{
    /// <summary>
    /// Maps a state name of the deploy cluster saga; <c>null</c> for states that do not start a phase.
    /// </summary>
    public static DeploymentPhase? FromSagaState(string? state) => state switch
    {
        "CommittingCluster" => DeploymentPhase.Committing,
        "AddingClusterNodes" => DeploymentPhase.AddingNodes,
        "UpdatingDeployments" => DeploymentPhase.UpdatingDeployments,
        "CleaningClusterNodes" or "CleaningCluster" => DeploymentPhase.CleaningUp,
        "RollingBackDeployments" or "RollingBackClusterNodes" or "RollingBackCommit" => DeploymentPhase.RollingBack,
        _ => null,
    };
}
