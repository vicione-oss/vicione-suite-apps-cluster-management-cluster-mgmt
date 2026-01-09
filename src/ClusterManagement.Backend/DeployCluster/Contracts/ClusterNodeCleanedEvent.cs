using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

public sealed record ClusterNodeCleanedEvent(Guid CorrelationId, Guid ClusterId, Guid NodeId) : IEvent, CorrelatedBy<Guid>;
