using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Public.Events;

/// <summary>
/// The steps an instance expects its deployment update to take, in the order it reports them with
/// <see cref="ClusterNodeDeploymentStepChanged"/>, from the first step on. Published once the update is planned and again
/// whenever the instance learns something that changes the rest of the list, so the latest one replaces the earlier ones.
/// Published for the UI only and on a best effort basis. The correlation id is the deployment saga's, the same for the
/// update and its rollback.
/// </summary>
[ForwardToUI]
public sealed record ClusterNodeDeploymentPlanned(
    Guid CorrelationId,
    Guid ClusterId,
    Guid ClusterApplicationId,
    IReadOnlyList<ClusterNodeDeploymentPlannedStep> Steps) : IEvent, CorrelatedBy<Guid>;
