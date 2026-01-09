using MassTransit;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

public sealed record ClusterNodeDeploymentsUpdatedEvent(Guid ClusterId, Guid ClusterApplicationId) : CorrelatedBy<Guid>
{
    public Guid CorrelationId => ClusterId;
}
