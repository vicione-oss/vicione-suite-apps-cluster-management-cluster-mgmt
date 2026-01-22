using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Events;

[ForwardToUI]
public sealed record ClusterDependenciesUpdateFailed(Guid CorrelationId, ErrorInfo Error) : IEvent, CorrelatedBy<Guid>;
