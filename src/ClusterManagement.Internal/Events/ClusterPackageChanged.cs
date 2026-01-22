using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Events;

[ForwardToUI]
public sealed record ClusterPackageChanged(string PackageName, string Version, ClusterPackageChange Change) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; }
}

public enum ClusterPackageChange
{
    Add,
    Delete,
    Update,
}
