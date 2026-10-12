namespace ClusterManagement.Public.Enums;

public enum ClusterNodeDeploymentStepStatus
{
    Started,
    Succeeded,
    Failed,

    /// <summary>
    /// The engine host answered that it did not start or stop the engine; the deployment goes on.
    /// </summary>
    Refused,
}
