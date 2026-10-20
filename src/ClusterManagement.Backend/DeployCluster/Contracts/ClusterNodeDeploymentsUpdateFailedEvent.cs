using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

public sealed record ClusterNodeDeploymentsUpdateFailedEvent(Guid CorrelationId, Guid ClusterId, Guid ClusterApplicationId, Version ClusterVersion, ErrorInfo ErrorInfo) : CorrelatedBy<Guid>;
