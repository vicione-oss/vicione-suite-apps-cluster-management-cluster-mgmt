using ClusterManagement.Public.Events;

namespace ClusterManagement.Public.Requests;

public record DeploymentState(Guid ClusterId, string CurrentState, Version ActiveVersion, Version TargetVersion)
{
    /// <summary>
    /// The correlation id of the deployment's events.
    /// </summary>
    public Guid CorrelationId { get; init; }

    /// <summary>
    /// The latest plan of the update and of the rollback of each instance that published one for this deployment, for a
    /// client that starts watching it while it runs; empty when the instance answering the request has not seen one.
    /// </summary>
    public IReadOnlyList<ClusterNodeDeploymentPlanned> Plans { get; init; } = [];
}
