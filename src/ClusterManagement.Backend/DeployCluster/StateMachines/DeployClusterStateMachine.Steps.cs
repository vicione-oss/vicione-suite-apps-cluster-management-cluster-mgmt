using ClusterManagement.Backend.DeployCluster.Extensions;
using MassTransit;

namespace ClusterManagement.Backend.DeployCluster.StateMachines;

/// <summary>
/// Reusable saga steps. Each step ends with exactly one transition or a finalize,
/// so the state table in the constructor reads as "event -> step".
/// </summary>
public sealed partial class DeployClusterStateMachine
{
    /// <summary>
    /// Publishes a <see cref="Public.Events.ClusterUpdateChanged"/> transition notification and transitions to <paramref name="state"/>.
    /// </summary>
    private EventActivityBinder<DeployClusterState, TMessage> TransitionWithNotification<TMessage>(
        EventActivityBinder<DeployClusterState, TMessage> binder, State state)
        where TMessage : class
        => binder
            .ThenAsync(ctx => PublishTransition(ctx, state.Name))
            .TransitionTo(state);

    /// <summary>
    /// Active and target cluster are known: add new nodes first, otherwise update the deployments right away.
    /// </summary>
    private EventActivityBinder<DeployClusterState, TMessage> ContinueWithTargetCluster<TMessage>(
        EventActivityBinder<DeployClusterState, TMessage> binder)
        where TMessage : class
        => binder.IfElse(ctx => ctx.Saga.ClusterNodesToCreate.Count > 0,
            StartAddingClusterNodes,
            StartUpdatingDeployments);

    private EventActivityBinder<DeployClusterState, TMessage> StartAddingClusterNodes<TMessage>(
        EventActivityBinder<DeployClusterState, TMessage> binder)
        where TMessage : class
        => TransitionWithNotification(binder.ThenAsync(SendAddClusterNodesCommand), AddingClusterNodes!);

    private EventActivityBinder<DeployClusterState, TMessage> StartUpdatingDeployments<TMessage>(
        EventActivityBinder<DeployClusterState, TMessage> binder)
        where TMessage : class
        => TransitionWithNotification(binder.PublishAsync(CreateUpdateDeploymentsTargetVersion), UpdatingDeployments!);

    /// <summary>
    /// All deployments replied: roll back if any failed, otherwise activate the target version and clean up.
    /// </summary>
    private EventActivityBinder<DeployClusterState, TMessage> ContinueAfterDeploymentsUpdated<TMessage>(
        EventActivityBinder<DeployClusterState, TMessage> binder)
        where TMessage : class
        => binder.IfElse(ctx => ctx.Saga.AtLeastOneDeployReturnedError,
            StartRollingBackDeployments,
            succeeded => succeeded
                .ThenAsync(UpdateActiveCluster)
                .IfElse(ctx => ctx.Saga.ClusterNodesToClean.Count > 0,
                    StartCleaningClusterNodes,
                    RemovePreviousVersionOrComplete));

    private EventActivityBinder<DeployClusterState, TMessage> StartCleaningClusterNodes<TMessage>(
        EventActivityBinder<DeployClusterState, TMessage> binder)
        where TMessage : class
        => TransitionWithNotification(
            binder.ThenAsync(ctx => SendCleanClusterNodesCommand(ctx, ctx.Saga.ClusterNodesToClean)),
            CleaningClusterNodes!);

    /// <summary>
    /// Remove the previous cluster version if requested, otherwise complete the deployment.
    /// </summary>
    private EventActivityBinder<DeployClusterState, TMessage> RemovePreviousVersionOrComplete<TMessage>(
        EventActivityBinder<DeployClusterState, TMessage> binder)
        where TMessage : class
        => binder.IfElse(ctx => ctx.Saga.ShouldRemovePreviousVersion(),
            removePrevious => TransitionWithNotification(removePrevious.ThenAsync(SendRemovePreviousVersion), CleaningCluster!),
            complete => complete
                .ThenAsync(CompleteClusterUpdate)
                .Finalize());

    /// <summary>
    /// Update the deployments back to the active version.
    /// </summary>
    private EventActivityBinder<DeployClusterState, TMessage> StartRollingBackDeployments<TMessage>(
        EventActivityBinder<DeployClusterState, TMessage> binder)
        where TMessage : class
        => TransitionWithNotification(
            binder
                .Then(ctx => ctx.Saga.ClusterApplicationsUpdateFinished.Clear())
                .PublishAsync(CreateUpdateDeploymentsActiveVersion),
            RollingBackDeployments!);

    /// <summary>
    /// Rollback replied: remove the nodes this saga created, then continue with the commit.
    /// </summary>
    private EventActivityBinder<DeployClusterState, TMessage> ContinueAfterDeploymentsRolledBack<TMessage>(
        EventActivityBinder<DeployClusterState, TMessage> binder)
        where TMessage : class
        => binder.IfElse(ctx => ctx.Saga.ClusterNodesCreated.Count > 0,
            StartRollingBackClusterNodes,
            ContinueAfterClusterNodesRolledBack);

    private EventActivityBinder<DeployClusterState, TMessage> StartRollingBackClusterNodes<TMessage>(
        EventActivityBinder<DeployClusterState, TMessage> binder)
        where TMessage : class
        => TransitionWithNotification(
            binder.ThenAsync(ctx => SendCleanClusterNodesCommand(ctx, [.. ctx.Saga.ClusterNodesCreated])),
            RollingBackClusterNodes!);

    /// <summary>
    /// Remove the version this saga committed, or fail if nothing was committed.
    /// </summary>
    private EventActivityBinder<DeployClusterState, TMessage> ContinueAfterClusterNodesRolledBack<TMessage>(
        EventActivityBinder<DeployClusterState, TMessage> binder)
        where TMessage : class
        => binder.IfElse(ctx => ctx.Saga.IsCommitVersionEmpty(),
            PublishFailedAndFinalize,
            removeCommit => TransitionWithNotification(removeCommit.ThenAsync(SendRemoveCommittedVersion), RollingBackCommit!));

    /// <summary>
    /// An activity of <paramref name="state"/> threw: publishes <see cref="Public.Events.ClusterUpdateFailed"/> with the error and finalizes the saga.
    /// </summary>
    private ExceptionActivityBinder<DeployClusterState, TMessage, Exception> FailDeployment<TMessage>(
        ExceptionActivityBinder<DeployClusterState, TMessage, Exception> binder, string state)
        where TMessage : class
        => binder
            .Then(ctx => LogDeploymentStepFailed(_logger, ctx.Exception, ctx.Saga.CorrelationId, ctx.Saga.ClusterId, state))
            .ThenAsync(ctx => PublishClusterUpdateFailed(ctx, ctx.Saga.ReportedTargetVersion(), GetFailureReason(ctx.Exception), state))
            .Then(BeforeFailedFinalize)
            .Finalize();

    /// <summary>
    /// An activity of <paramref name="state"/> threw after the saga committed a version: keeps the failure and removes the
    /// committed version in <see cref="RollingBackCommit"/>, which publishes <see cref="Public.Events.ClusterUpdateFailed"/>.
    /// </summary>
    private ExceptionActivityBinder<DeployClusterState, TMessage, Exception> FailAndRemoveCommittedVersion<TMessage>(
        ExceptionActivityBinder<DeployClusterState, TMessage, Exception> binder, string state)
        where TMessage : class
        => binder
            .Then(ctx => LogDeploymentStepFailed(_logger, ctx.Exception, ctx.Saga.CorrelationId, ctx.Saga.ClusterId, state))
            .Then(ctx => KeepFailure(ctx, GetFailureReason(ctx.Exception)))
            .ThenAsync(SendRemoveCommittedVersion)
            .ThenAsync(ctx => PublishTransition(ctx, RollingBackCommit!.Name))
            .TransitionTo(RollingBackCommit);

    /// <summary>
    /// The message of <paramref name="exception"/>, or of its inner exception if MassTransit wrapped it in an
    /// <see cref="EventExecutionException"/>, as it does for some activities, e.g. <c>PublishAsync</c>.
    /// </summary>
    private static string GetFailureReason(Exception exception)
        => (exception is EventExecutionException { InnerException: { } inner } ? inner : exception).Message;

    /// <summary>
    /// Publishes <see cref="Public.Events.ClusterUpdateFailed"/>, logs the results and finalizes the saga.
    /// </summary>
    private EventActivityBinder<DeployClusterState, TMessage> PublishFailedAndFinalize<TMessage>(
        EventActivityBinder<DeployClusterState, TMessage> binder)
        where TMessage : class
        => PublishFailedAndFinalize(binder, failedVersionKept: false);

    /// <inheritdoc cref="PublishFailedAndFinalize{TMessage}(EventActivityBinder{DeployClusterState, TMessage})"/>
    /// <param name="binder">The activities to add to.</param>
    /// <param name="failedVersionKept">The rollback could not delete the version this saga committed.</param>
    private EventActivityBinder<DeployClusterState, TMessage> PublishFailedAndFinalize<TMessage>(
        EventActivityBinder<DeployClusterState, TMessage> binder, bool failedVersionKept)
        where TMessage : class
        => binder
            .ThenAsync(ctx => PublishClusterUpdateFailed(ctx, failedVersionKept))
            .Then(BeforeFailedFinalize)
            .Finalize();
}
