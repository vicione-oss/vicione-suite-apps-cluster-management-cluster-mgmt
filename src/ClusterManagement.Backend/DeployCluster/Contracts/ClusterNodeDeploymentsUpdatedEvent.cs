using MassTransit;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

public sealed record ClusterNodeDeploymentsUpdatedEvent(Guid CorrelationId, Guid ClusterId, Guid ClusterApplicationId) : CorrelatedBy<Guid>;
