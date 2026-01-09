using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

[ForwardToUI]
public sealed record ClusterChanged(Guid CorrelationId, Guid ClusterId, CrudAction Action, Version? Version) : IEvent, CorrelatedBy<Guid>;
