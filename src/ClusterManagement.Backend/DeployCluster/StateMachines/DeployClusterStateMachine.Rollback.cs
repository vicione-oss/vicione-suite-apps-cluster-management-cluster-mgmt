using System.Runtime.ExceptionServices;
using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.DeployCluster.Extensions;
using MassTransit;

namespace ClusterManagement.Backend.DeployCluster.StateMachines;

/// <summary>
/// The rollback, for a failed deployment and for an activity that threw. The activities of a <c>Catch</c> run on the fault path,
/// where a nested <c>Catch</c> handles the original exception again instead of a new one, so a rollback that cannot start
/// is handled in code here. Every rollback starts through <see cref="RollBack"/>, which keeps the order of the steps in one place.
/// </summary>
/// <remarks>
/// The Suite buffers what a saga publishes or sends until it handled the message (in memory outbox, ADR-004 D1),
/// so a publish or send does not throw here: only loading the active version can.
/// </remarks>
public sealed partial class DeployClusterStateMachine
{
    /// <summary>
    /// An activity of <paramref name="state"/> threw: keeps the failure and rolls back. The deployments are only rolled back
    /// if they were updated, see <paramref name="deploymentsUpdated"/>. Otherwise the rollback starts with the created nodes.
    /// </summary>
    private ExceptionActivityBinder<DeployClusterState, TMessage, Exception> FailAndRollBack<TMessage>(
        ExceptionActivityBinder<DeployClusterState, TMessage, Exception> binder, string state, bool deploymentsUpdated = false)
        where TMessage : class
        => LogStepFailed(binder, state)
            .Then(ctx => KeepFailure(ctx, GetFailureReason(ctx.Exception)))
            .ThenAsync(ctx => RollBack(ctx, deploymentsUpdated ? RollingBackDeployments! : RollingBackClusterNodes!));

    /// <summary>
    /// An activity of <paramref name="state"/> threw while the deployments are updated: rolls back unless the target version
    /// was activated. Then the deployment succeeded and only a cleanup failed, so it completes.
    /// </summary>
    private ExceptionActivityBinder<DeployClusterState, TMessage, Exception> FailAndRollBackUnlessActivated<TMessage>(
        ExceptionActivityBinder<DeployClusterState, TMessage, Exception> binder, string state)
        where TMessage : class
        => binder.IfElse(ctx => IsTargetVersionActivated(ctx.Saga),
            activated => CompleteAfterFailedCleanup(activated, state),
            notActivated => FailAndRollBack(notActivated, state, deploymentsUpdated: true));

    /// <summary>
    /// The target version is active and every application runs it. That also holds for a redeploy of the active version
    /// once every application updated without a failure, so it completes even if activating it again throws.
    /// A redeploy with a failed or missing reply is rolled back like any other deployment.
    /// </summary>
    private static bool IsTargetVersionActivated(DeployClusterState saga)
        => saga.IsTargetVersionActive()
        && !saga.AtLeastOneDeployReturnedError
        && saga.ClusterApplicationsToUpdate.All(saga.ClusterApplicationsUpdateFinished.ContainsKey);

    /// <summary>
    /// An activity of <paramref name="state"/> threw after the target version was activated: the deployment succeeded,
    /// so the failed cleanup is logged and the saga completes.
    /// </summary>
    private ExceptionActivityBinder<DeployClusterState, TMessage, Exception> CompleteAfterFailedCleanup<TMessage>(
        ExceptionActivityBinder<DeployClusterState, TMessage, Exception> binder, string state)
        where TMessage : class
        => LogStepFailed(binder, state)
            .ThenAsync(CompleteClusterUpdate)
            .Finalize();

    /// <summary>
    /// Logs the failed step. A cancellation is thrown again, see <see cref="IsCancellation"/>.
    /// </summary>
    private ExceptionActivityBinder<DeployClusterState, TMessage, Exception> LogStepFailed<TMessage>(
        ExceptionActivityBinder<DeployClusterState, TMessage, Exception> binder, string state)
        where TMessage : class
        => binder
            .Then(ctx =>
            {
                if (IsCancellation(ctx.Exception, ctx.CancellationToken))
                    ExceptionDispatchInfo.Throw(ctx.Exception);
            })
            .Then(ctx => LogDeploymentStepFailed(_logger, ctx.Exception, ctx.Saga.CorrelationId, ctx.Saga.ClusterId, state));

    /// <summary>
    /// The message was cancelled, e.g. as the host shuts down. That is no failed step: the message faults instead and the saga
    /// keeps its state, so its state timeout continues the deployment later, instead of skipping the rest of the rollback now.
    /// </summary>
    internal static bool IsCancellation(Exception exception, CancellationToken cancellationToken)
        => exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    /// <summary>
    /// Starts the first rollback step that applies, beginning at <paramref name="firstStep"/>: the deployments, the created nodes,
    /// then the committed version. Finalizes with <see cref="Public.Events.ClusterUpdateFailed"/> once no step is left,
    /// or if the deployments cannot be rolled back, see <see cref="TryCreateRollback"/>.
    /// </summary>
    private async Task RollBack<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx, State firstStep)
        where TMessage : class
    {
        if (firstStep == RollingBackDeployments)
        {
            if (await TryCreateRollback(ctx) is not { } rollback)
            {
                await FinalizeFailed(ctx, failedVersionKept: !ctx.Saga.IsCommitVersionEmpty());
                return;
            }

            // The update results are only replaced by the rollback results once the rollback can start,
            // so a rollback that cannot start reports the errors of the failed applications
            ctx.Saga.ClusterApplicationsUpdateFinished.Clear();
            await ctx.Publish(rollback);
            await StartRollbackStep(ctx, RollingBackDeployments!);
            return;
        }

        if (firstStep != RollingBackCommit && ctx.Saga.ClusterNodesCreated.Count > 0)
        {
            await SendCleanClusterNodesCommand(ctx, [.. ctx.Saga.ClusterNodesCreated]);
            await StartRollbackStep(ctx, RollingBackClusterNodes!);
            return;
        }

        if (!ctx.Saga.IsCommitVersionEmpty())
        {
            await SendRemoveCommittedVersion(ctx);
            await StartRollbackStep(ctx, RollingBackCommit!);
            return;
        }

        await FinalizeFailed(ctx, failedVersionKept: false);
    }

    /// <summary>
    /// None of the instances answered the update, so the rollback is sent for when they come back, but nothing waits for it.
    /// The rollback continues with the created nodes, unless the deployments cannot be rolled back, see <see cref="TryCreateRollback"/>.
    /// </summary>
    private async Task RollBackWithoutWaiting<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class
    {
        if (await TryCreateRollback(ctx) is not { } rollback)
        {
            await FinalizeFailed(ctx, failedVersionKept: !ctx.Saga.IsCommitVersionEmpty());
            return;
        }

        await ctx.Publish(rollback);
        await RollBack(ctx, RollingBackClusterNodes!);
    }

    /// <summary>
    /// Loads the active version to roll the deployments back to, or returns null if that throws. The applications that updated
    /// then keep running the committed version and its nodes, so the rollback stops and neither is removed.
    /// </summary>
    private async Task<UpdateDeployments?> TryCreateRollback<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class
    {
        try
        {
            return await CreateUpdateDeploymentsActiveVersion(ctx);
        }
        catch (Exception ex) when (!IsCancellation(ex, ctx.CancellationToken))
        {
            LogDeploymentStepFailed(_logger, ex, ctx.Saga.CorrelationId, ctx.Saga.ClusterId, nameof(RollingBackDeployments));
            return null;
        }
    }

    /// <summary>
    /// Once its command went out, the saga waits for the replies in <paramref name="state"/>.
    /// </summary>
    private async Task StartRollbackStep<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx, State state)
        where TMessage : class
    {
        await PublishTransition(ctx, state.Name);
        await ctx.TransitionToState(state);
    }

    /// <summary>
    /// Publishes <see cref="Public.Events.ClusterUpdateFailed"/> and finalizes the saga.
    /// </summary>
    /// <param name="ctx">The saga context.</param>
    /// <param name="failedVersionKept">The rollback did not remove the version this saga committed.</param>
    private async Task FinalizeFailed<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx, bool failedVersionKept)
        where TMessage : class
    {
        await PublishClusterUpdateFailed(ctx, failedVersionKept);

        BeforeFailedFinalize(ctx);
        await ctx.TransitionToState(Final);
    }
}
