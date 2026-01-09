using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

public sealed record ClusterNodeCleanedEvent(Guid ClusterId, Guid NodeId) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId => ClusterId;
}
