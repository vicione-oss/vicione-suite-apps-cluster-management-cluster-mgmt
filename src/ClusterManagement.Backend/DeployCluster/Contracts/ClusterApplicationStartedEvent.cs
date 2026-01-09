using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.DeployCluster.Contracts;

public sealed record ClusterApplicationStartedEvent(Guid ClusterId, Guid ClusterApplicationId) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId => ClusterId;
}
