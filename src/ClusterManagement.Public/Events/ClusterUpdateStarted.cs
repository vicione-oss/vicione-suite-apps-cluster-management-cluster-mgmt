using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

[ForwardToUI]
public sealed record ClusterUpdateStarted(Guid ClusterId, Version TargetVersion) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId => ClusterId;

    public Dictionary<Guid, Guid?> NodeApplications { get; init; } = [];
    public List<Guid> NodesToCreate { get; init; } = [];
    public List<Guid> NodesToClean { get; init; } = [];

    // these should be requested by ui
    public Dictionary<Guid, string> Nodes { get; init; } = [];
    public Dictionary<Guid, string> Applications { get; init; } = [];
}
