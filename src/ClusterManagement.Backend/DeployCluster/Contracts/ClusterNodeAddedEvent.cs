using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

public sealed record ClusterNodeAddedEvent(Guid CorrelationId, Guid ClusterId, Guid ClusterNodeId) : IEvent, CorrelatedBy<Guid>;
