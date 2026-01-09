using ClusterManagement.Public.Enums;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

[ForwardToUI]
public sealed record ClusterUpdateChanged(Guid CorrelationId, Guid ClusterId, Version TargetVersion, string? CurrentState) : IEvent, CorrelatedBy<Guid>
{
    public required ClusterUpdateChange Change { get; init; }

    public Guid? NodeId { get; init; }

    public Guid? ApplicationId { get; init; }

    public ErrorInfo? Error { get; init; }
}
