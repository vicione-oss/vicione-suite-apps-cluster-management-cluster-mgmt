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
}
