using ClusterManagement.Backend.DeployCluster.Contracts;
using MassTransit;

namespace ClusterManagement.Backend.DeployCluster.StateMachines;

public sealed partial class DeployClusterStateMachine
{
    /// <summary>
    /// Sent by <see cref="DeploySagaTimeoutWatchdog"/> once <see cref="DeployClusterState.StateTimeoutAt"/> has passed.
    /// If the saga is still in that state (e.g. a node is offline and never answers) it rolls back or finalizes instead of waiting forever.
    /// </summary>
    public Event<DeployClusterStateTimeout>? StateTimedOut { get; }

    private void ConfigureStateTimeouts()
    {
        Event(() => StateTimedOut);

        State?[] states = [CommittingCluster, AddingClusterNodes, UpdatingDeployments, CleaningClusterNodes,
            CleaningCluster, RollingBackDeployments, RollingBackClusterNodes, RollingBackCommit];

        // Entering a state replaces the timeout of the previous state
        foreach (var state in states)
            WhenEnter(state, binder => binder.Then(ctx => ctx.Saga.StateTimeoutAt = DateTime.UtcNow + GetStateTimeout(state!.Name)));

        HandleStateTimeouts();

        // The watchdog found the saga overdue, but it entered another state before the timeout arrived
        DuringAny(
            Ignore(StateTimedOut, ctx => !IsStateOverdue(ctx.Saga)));
    }

    // A timeout that faults would leave the saga overdue, and the watchdog would send it again on every check,
    // so the rollback handles an active version that cannot be loaded, see RollBack
    private void HandleStateTimeouts()
    {
        // Nothing was deployed yet
        During(CommittingCluster,
            PublishFailedAndFinalize(WhenStateTimedOut()
                .Then(SetTimeoutFailureReason)));

        // Nothing was deployed yet, remove nodes created so far and the committed version
        During(AddingClusterNodes,
            WhenStateTimedOut()
                .Then(SetTimeoutFailureReason)
                .ThenAsync(ctx => RollBack(ctx, RollingBackClusterNodes!)));

        // Treat missing responses as failed deployments -> rollback to active version.
        // Instances that didn't answer the update, e.g. offline nodes, are unlikely to answer the rollback either:
        // they get the rollback command for when they come back, but the rollback only waits for the others.
        During(UpdatingDeployments,
            WhenStateTimedOut()
                .Then(SetTimeoutFailureReason)
                .Then(ctx => ctx.Saga.AtLeastOneDeployReturnedError = true)
                .Then(ctx => ctx.Saga.ClusterApplicationsToUpdate.RemoveAll(id => !ctx.Saga.ClusterApplicationsUpdateFinished.ContainsKey(id)))
                .IfElse(ctx => ctx.Saga.ClusterApplicationsToUpdate.Count > 0,
                    answered => answered.ThenAsync(ctx => RollBack(ctx, RollingBackDeployments!)),
                    noneAnswered => noneAnswered.ThenAsync(RollBackWithoutWaiting)));

        // Active cluster was already updated, the deployment itself succeeded
        During(CleaningClusterNodes, CleaningCluster,
            WhenStateTimedOut()
                .Then(LogTimeout)
                .ThenAsync(CompleteClusterUpdate)
                .Finalize());

        // Give up waiting for the rollback, but still remove the created nodes and the committed version
        During(RollingBackDeployments,
            WhenStateTimedOut()
                .Then(SetTimeoutFailureReason)
                .ThenAsync(ctx => RollBack(ctx, RollingBackClusterNodes!)));

        // Give up waiting for the created nodes, but still remove the committed version
        During(RollingBackClusterNodes,
            WhenStateTimedOut()
                .Then(SetTimeoutFailureReason)
                .ThenAsync(ctx => RollBack(ctx, RollingBackCommit!)));

        // Giving up waiting for the removal of the committed version, which may be kept
        During(RollingBackCommit,
            PublishFailedAndFinalize(WhenStateTimedOut()
                .Then(SetTimeoutFailureReason), failedVersionKept: true));
    }

    private EventActivityBinder<DeployClusterState, DeployClusterStateTimeout> WhenStateTimedOut()
        => When(StateTimedOut, ctx => IsStateOverdue(ctx.Saga));

    private static bool IsStateOverdue(DeployClusterState saga)
        => saga.StateTimeoutAt <= DateTime.UtcNow;

    private TimeSpan GetStateTimeout(string state)
        => state switch
        {
            nameof(CommittingCluster) => _timeouts.CommittingCluster,
            nameof(AddingClusterNodes) => _timeouts.AddingClusterNodes,
            nameof(UpdatingDeployments) => _timeouts.UpdatingDeployments,
            nameof(CleaningClusterNodes) => _timeouts.CleaningClusterNodes,
            nameof(CleaningCluster) => _timeouts.CleaningCluster,
            nameof(RollingBackDeployments) => _timeouts.RollingBackDeployments,
            nameof(RollingBackClusterNodes) => _timeouts.RollingBackClusterNodes,
            nameof(RollingBackCommit) => _timeouts.RollingBackCommit,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "No timeout defined for state"),
        };

    private void LogTimeout(BehaviorContext<DeployClusterState, DeployClusterStateTimeout> ctx)
    {
        var state = GetState(ctx.Saga.CurrentState);
        LogStateTimeout(_logger, ctx.Saga.CorrelationId, state, GetStateTimeout(state), GetPendingApplications(ctx.Saga));
    }

    private void SetTimeoutFailureReason(BehaviorContext<DeployClusterState, DeployClusterStateTimeout> ctx)
    {
        LogTimeout(ctx);

        // Keep the first failure, e.g. a timeout in UpdatingDeployments followed by a timeout in RollingBackDeployments,
        // or failed application updates followed by a timeout of their rollback, as KeepFailure does
        if (ctx.Saga.AtLeastOneDeployReturnedError)
            return;

        var state = GetState(ctx.Saga.CurrentState);
        ctx.Saga.FailureReason ??= $"Timeout after {GetStateTimeout(state)} in state {state}";
    }

    private static string GetPendingApplications(DeployClusterState saga)
        => string.Join(", ", saga.ClusterApplicationsToUpdate.Where(id => !saga.ClusterApplicationsUpdateFinished.ContainsKey(id)));
}
