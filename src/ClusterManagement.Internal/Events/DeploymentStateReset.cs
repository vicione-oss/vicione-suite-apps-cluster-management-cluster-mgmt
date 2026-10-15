using ClusterManagement.Public.Requests;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Events;

/// <summary>
/// The outcome of <see cref="Commands.ResetSagaState"/>, with its correlation id.
/// </summary>
[ForwardToUI]
public sealed record DeploymentStateReset(Guid CorrelationId, Guid ClusterId) : IEvent, CorrelatedBy<Guid>
{
    /// <summary>
    /// The deployments whose state was removed, each with the version it deployed as its target; empty when there was
    /// nothing to reset.
    /// </summary>
    public List<DeploymentState> Reset { get; init; } = [];

    /// <summary>
    /// Why the reset failed; nothing was reset then.
    /// </summary>
    public ErrorInfo? Error { get; init; }
}
