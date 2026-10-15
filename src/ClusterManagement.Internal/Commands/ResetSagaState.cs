using Sdk.Messaging;

namespace ClusterManagement.Internal.Commands;

/// <summary>
/// Removes every deployment state of a cluster and ends each removed deployment as failed. On an endpoint of its own, so
/// it still gets through while a hung deployment blocks the endpoint the other commands share; the outcome comes back as
/// <see cref="Events.DeploymentStateReset"/>.
/// </summary>
/// <param name="ClusterId">The cluster whose deployment states are removed.</param>
/// <param name="RequestedBy">The name of the user who resets them, for the log.</param>
[MessageEndpoint(nameof(ResetSagaState))]
public sealed record ResetSagaState(Guid ClusterId, string RequestedBy) : ICommand
{
    /// <summary>
    /// The reason of the <c>ClusterUpdateFailed</c> that ends each deployment the reset removed.
    /// </summary>
    public const string FailureReason = "The deployment state was reset";

    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
