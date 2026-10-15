using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

[ForwardToUI]
public sealed record ClusterUpdateFailed(Guid CorrelationId, Guid ClusterId, Version Version) : IEvent, CorrelatedBy<Guid>
{
    public Dictionary<Guid, ErrorInfo> ErrorsByApplication { get; init; } = [];

    /// <summary>
    /// A reason why the cluster update failed
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// The failing state of cluster deployment
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Duration of failed cluster deployment
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// The deployment did not fail on its own: its state was reset, and this event only tells whoever waits for it to
    /// stop waiting.
    /// </summary>
    public bool WasReset { get; init; }

    /// <summary>
    /// The rollback could not delete the failed version from the cluster store: the instances run the previous version
    /// again, but the failed version is still stored.
    /// </summary>
    public bool FailedVersionKept { get; init; }
}
