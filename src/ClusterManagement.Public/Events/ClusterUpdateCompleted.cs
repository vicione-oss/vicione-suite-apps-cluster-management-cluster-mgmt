using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

[ForwardToUI]
public sealed record ClusterUpdateCompleted(Guid CorrelationId, Guid ClusterId, Version NewVersion, TimeSpan Duration) : IEvent, CorrelatedBy<Guid>;
