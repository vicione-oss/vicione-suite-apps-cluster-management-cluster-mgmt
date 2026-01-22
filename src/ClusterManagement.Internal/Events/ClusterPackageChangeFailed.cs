using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Events;

[ForwardToUI]
public sealed record ClusterPackageChangeFailed(Guid CorrelationId, ErrorInfo Error, ClusterPackageChange Change, string? PackageName = null) : IEvent, CorrelatedBy<Guid>;
