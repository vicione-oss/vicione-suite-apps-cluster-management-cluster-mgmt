using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.DeployCluster.Extensions;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.DeployCluster.StateMachines;

public sealed partial class DeployClusterStateMachine
{
    private Task PublishTransition<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx, string targetTransition) where TMessage : class
    {
        var ev = new ClusterUpdateChanged(ctx.Saga.CorrelationId, ctx.Saga.ClusterId, ctx.Saga.TargetVersion, targetTransition) { Change = ClusterUpdateChange.Transition };

        LogTransition(_logger,
            ctx.Saga.CorrelationId,
            GetState(ctx.Saga.CurrentState),
            targetTransition);

        return ctx.Publish(ev);
    }

    private Task PublishDeployClusterRejected(BehaviorContext<DeployClusterState, Public.Commands.DeployCluster> ctx)
    {
        // Important to correlate the reply to the command and not to the ongoing saga correlation
        // to allow the caller to react on rejecting his command 
        var ev = new ClusterUpdateRejected(ctx.Message.CorrelationId, ctx.Saga.ClusterId, ctx.Saga.ActiveVersion, ctx.Saga.TargetVersion, ctx.Message.TargetVersion);

        LogDeployRejected(_logger,
            ctx.Saga.CorrelationId,
            ctx.Saga.TargetVersion,
            ctx.Message.TargetVersion);

        return ctx.Publish(ev);
    }

    /// <summary>
    /// Publish <see cref="ClusterUpdateFailed"/> containing the errors occurred on update deployments step
    /// </summary>
    private async Task PublishClusterUpdateFailed<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class
    {
        // If we fail before the cluster was commited and set as target version
        var failedVersion = ctx.Saga.IsCommitVersionEmpty() ? ctx.Saga.TargetVersion : ctx.Saga.CommitVersion;

        var failed = new ClusterUpdateFailed(ctx.Saga.CorrelationId, ctx.Saga.ClusterId, failedVersion)
        {
            Duration = DateTime.UtcNow - ctx.Saga.Started,
            ErrorsByApplication = ctx.Saga.ClusterApplicationsUpdateFinished
                .Where(v => v.Value is not null)
                .ToDictionary(k => k.Key, v => v.Value!),
            State = GetState(ctx.Saga.CurrentState),
            Reason = "At least one application update failed"
        };

        LogClusterUpdatedFailed(_logger,
            ctx.Saga.CorrelationId,
            failedVersion);

        await ctx.Publish(failed, ctx.CancellationToken);
    }

    private async Task SendCommitClusterCommand(BehaviorContext<DeployClusterState, Public.Commands.DeployCluster> ctx)
    {
        if (!ctx.CorrelationId.HasValue)
            throw new InvalidOperationException("CorrelationId is null.");

        if (ctx.Message.Options is null)
            throw new InvalidOperationException("Options not available.");

        if (ctx.Saga.IsCommitVersionEmpty())
            throw new InvalidOperationException("Commit version is empty.");

        var metaInfo = new CommitClusterMetaInfo
        {
            ClusterId = ctx.Saga.ClusterId,
            ClusterVersion = ctx.Saga.CommitVersion,
        };

        var command = new CommitClusterChange(ctx.Message.Options.CompressedCluster, metaInfo, null) { CorrelationId = ctx.CorrelationId.Value };

        LogSendCommitVersion(_logger, ctx.Saga.CorrelationId, ctx.Saga.CommitVersion);

        await ctx.Send(MessagingHelper.GetCommandEndpointAddress<CommitClusterChange>(), command);
    }

    /// <summary>
    /// Sends a <see cref="RemoveCluster" /> command to remove the cluster version that was created.
    /// Does nothing if no cluster was committed by the saga.
    /// </summary>    
    private async Task SendRemoveCommittedVersion<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class
    {
        if (!ctx.CorrelationId.HasValue)
            throw new InvalidOperationException("CorrelationId is null.");

        var command = new RemoveCluster(ctx.Saga.ClusterId, ctx.Saga.CommitVersion)
        {
            CorrelationId = ctx.CorrelationId.Value,
        };

        LogSendRemoveCommittedVersion(_logger, ctx.Saga.CorrelationId, command.Version);

        await ctx.Send(MessagingHelper.GetCommandEndpointAddress<RemoveCluster>(), command);
    }

    /// <summary>
    /// Sends a <see cref="RemoveCluster" /> command to remove the previous cluster version.    
    /// </summary>    
    private async Task SendRemovePreviousVersion<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class
    {
        if (!ctx.CorrelationId.HasValue)
            throw new InvalidOperationException("CorrelationId is null.");

        ArgumentNullException.ThrowIfNull(ctx.Saga.RemoveVersion);

        var command = new RemoveCluster(ctx.Saga.ClusterId, ctx.Saga.RemoveVersion)
        {
            CorrelationId = ctx.CorrelationId.Value,
        };

        LogSendRemovePreviousVersion(_logger, ctx.Saga.CorrelationId, command.Version);

        await ctx.Send(MessagingHelper.GetCommandEndpointAddress<RemoveCluster>(), command);
    }

    private static Task SendAddClusterNodesCommand<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class => ctx.Send(MessagingHelper.GetCommandEndpointAddress<AddClusterNodesCommand>(),
                            new AddClusterNodesCommand(ctx.Saga.CorrelationId, ctx.Saga.ClusterId, ctx.Saga.ClusterNodesToCreate));

    private static Task SendCleanClusterNodesCommand<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx, List<Guid> nodesToClean)
        where TMessage : class => ctx.Send(MessagingHelper.GetCommandEndpointAddress<CleanClusterNodesCommand>(),
            new CleanClusterNodesCommand(ctx.Saga.CorrelationId, ctx.Saga.ClusterId, [.. nodesToClean]));

    private Task<UpdateDeployments> CreateUpdateDeploymentsTargetVersion<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class => CreateUpdateDeploymentsEvent(ctx.Saga.CorrelationId, ctx.Saga.ClusterId, ctx.Saga.TargetVersion, ctx.CancellationToken);

    private Task<UpdateDeployments> CreateUpdateDeploymentsActiveVersion<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class
        => CreateUpdateDeploymentsEvent(ctx.Saga.CorrelationId, ctx.Saga.ClusterId, ctx.Saga.ActiveVersion, ctx.CancellationToken);

    private async Task<UpdateDeployments> CreateUpdateDeploymentsEvent(Guid correlationId, Guid clusterId, Version version, CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var snapshotStore = scope.ServiceProvider.GetRequiredService<IClusterSnapshotStore>();
        var targetCluster = await snapshotStore.LoadCompressedJson(clusterId, version, cancellationToken) ??
            throw new ArgumentException($"Cluster {clusterId} v{version} not found"); //Will throw in IClusterSnapshotStore

        return new(targetCluster, correlationId, clusterId);
    }
}
