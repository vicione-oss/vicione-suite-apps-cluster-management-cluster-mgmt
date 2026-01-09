using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

[ForwardToUI]
public sealed record CommitClusterChangeFailed(Guid CorrelationId, Guid ClusterId, Version Version, ErrorInfo Error) : IEvent, CorrelatedBy<Guid>;
