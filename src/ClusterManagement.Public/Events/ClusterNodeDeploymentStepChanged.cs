using ClusterManagement.Public.Enums;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

/// <summary>
/// An instance started, finished or failed a step of its deployment update, or the engine host refused it. Published for
/// the UI only and on a best effort basis, so a missing event says nothing about the deployment's outcome. The
/// correlation id is the deployment saga's, the same for the update and its rollback.
/// </summary>
[ForwardToUI]
public sealed record ClusterNodeDeploymentStepChanged(
    Guid CorrelationId,
    Guid ClusterId,
    Guid ClusterApplicationId,
    ClusterNodeDeploymentStep Step,
    ClusterNodeDeploymentStepStatus Status) : IEvent, CorrelatedBy<Guid>
{
    /// <summary>
    /// The step belongs to the rollback that restores the version that ran before, not to the update.
    /// </summary>
    public bool IsRollback { get; init; }

    /// <summary>
    /// <c>null</c> for a step of the whole instance, such as preparing or planning.
    /// </summary>
    public Guid? EngineHostId { get; init; }

    /// <summary>
    /// <c>null</c> for a step without an engine host and for an engine host the target cluster does not contain.
    /// </summary>
    public string? EngineHostName { get; init; }

    /// <summary>
    /// <c>null</c> for a step without an engine and for an engine the instance does not know, such as one left over on an
    /// engine host.
    /// </summary>
    public Guid? EngineId { get; init; }

    /// <summary>
    /// <c>null</c> for a step without an engine and for an engine the target cluster does not contain.
    /// </summary>
    public string? EngineName { get; init; }

    /// <summary>
    /// When the instance reached <see cref="Status"/>, by its own clock: compare it only with other times of the same
    /// instance, as the clocks of the instances and their clients can differ.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// The error the instance reports for its deployment update; set only when <see cref="Status"/> is
    /// <see cref="ClusterNodeDeploymentStepStatus.Failed"/>.
    /// </summary>
    public ErrorInfo? Error { get; init; }
}
