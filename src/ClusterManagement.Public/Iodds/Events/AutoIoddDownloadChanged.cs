using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Public.Iodds.Events;

[ForwardToUI]
public sealed record AutoIoddDownloadChanged(bool Value, ErrorInfo? Error = null) : IEvent, CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
