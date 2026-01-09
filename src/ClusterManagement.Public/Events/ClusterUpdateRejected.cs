using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

[ForwardToUI]
public sealed record ClusterUpdateRejected(Guid ClusterId, Version ActiveVersion, Version TargetVersion, Version RejectedVersion) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId => ClusterId;
}
