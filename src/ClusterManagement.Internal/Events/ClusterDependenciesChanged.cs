using ClusterManagement.Internal.Contracts;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Events;

[ForwardToUI]
public sealed record ClusterDependenciesChanged(IReadOnlyList<ClusterDependencyChange> Changes) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}

public record ClusterDependencyChange(CrudAction Action, ClusterDependencyOperation Operation);
