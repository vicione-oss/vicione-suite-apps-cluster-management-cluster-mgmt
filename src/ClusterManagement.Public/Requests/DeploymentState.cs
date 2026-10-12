namespace ClusterManagement.Public.Requests;

public record DeploymentState(Guid ClusterId, string CurrentState, Version ActiveVersion, Version TargetVersion)
{
    /// <summary>
    /// The correlation id of the deployment's events.
    /// </summary>
    public Guid CorrelationId { get; init; }
}
