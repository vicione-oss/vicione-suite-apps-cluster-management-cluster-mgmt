using ClusterManagement.Public.Enums;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

[ForwardToUI]
public sealed record ClusterUpdateChanged(Guid ClusterId, Version TargetVersion, string? CurrentState) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId => ClusterId;

    public required ClusterUpdateChange Change { get; init; }

    public Guid? NodeId { get; init; }

    public Guid? ApplicationId { get; init; }

    public ErrorInfo? Error { get; init; }
}
