using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

public sealed record ClusterNodeDeploymentsUpdateFailedEvent(Guid ClusterId, Guid ClusterApplicationId, ErrorInfo ErrorInfo) : CorrelatedBy<Guid>
{
    public Guid CorrelationId => ClusterId;
}
