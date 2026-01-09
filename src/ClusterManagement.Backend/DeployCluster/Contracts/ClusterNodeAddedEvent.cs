using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

public sealed record ClusterNodeAddedEvent(Guid ClusterId, Guid ClusterNodeId) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId => ClusterId;
}
