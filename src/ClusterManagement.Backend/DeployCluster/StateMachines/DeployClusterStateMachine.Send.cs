using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.DeployCluster.StateMachines;

public sealed partial class DeployClusterStateMachine
{
    private Task PublishTransition<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx, string targetTransition) where TMessage : class
    {
        var ev = new ClusterUpdateChanged(ctx.Saga.ClusterId, ctx.Saga.TargetVersion, targetTransition) { Change = ClusterUpdateChange.Transition };

        _logger.LogInformation("Saga[{CorrelationId}] transition from {OldState} to {State}",
            ctx.Saga.CorrelationId,
            GetState(ctx.Saga.CurrentState),
            targetTransition);

        return ctx.Publish(ev);
    }

    private Task PublishDeployClusterRejected(BehaviorContext<DeployClusterState, Public.Commands.DeployCluster> ctx)
    {
        var ev = new ClusterUpdateRejected(ctx.Saga.ClusterId, ctx.Saga.ActiveVersion, ctx.Saga.TargetVersion, ctx.Message.TargetVersion);

        _logger.LogInformation("Saga[{CorrelationId}] Deployment of target version {Version} is in progress. Deployment of version {RejectVersion} rejected",
            ctx.Saga.CorrelationId,
            ctx.Saga.TargetVersion,
            ctx.Message.TargetVersion);

        return ctx.Publish(ev);
    }

    /// <summary>
    /// Publish <see cref="ClusterUpdateFailed"/> containing the errors occurred on update deployments step
    /// </summary>
    private static async Task PublishClusterUpdateFailed<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class
    {
        var failed = new ClusterUpdateFailed(ctx.Saga.ClusterId, ctx.Saga.TargetVersion)
        {
            Duration = DateTime.UtcNow - ctx.Saga.Started,
            ErrorsByApplication = ctx.Saga.ClusterApplicationsUpdateFinished
                .Where(v => v.Value is not null)
                .ToDictionary(k => k.Key, v => v.Value!),
            State = GetState(ctx.Saga.CurrentState),
            Reason = "At least one application update failed"
        };

        await ctx.Publish(failed, ctx.CancellationToken);
    }

    private async Task SendCommitClusterCommand(BehaviorContext<DeployClusterState, Public.Commands.DeployCluster> ctx)
    {
        if (ctx.Message.Options is null)
            throw new InvalidOperationException("Options not initialized");

        if (ctx.CorrelationId is null)
            throw new InvalidOperationException("Correlation");

        var snapshotStore = _services.GetRequiredService<IClusterSnapshotStore>();
        var nextVersion = await snapshotStore.GetLatestVersionIncrement(ctx.Saga.ClusterId, VersionIncrementType.Patch);

        var metaInfo = new CommitClusterMetaInfo()
        {
            ClusterId = ctx.Saga.ClusterId,
            ClusterVersion = nextVersion,
        };

        var command = new CommitClusterChange(ctx.Message.Options.CompressedCluster, metaInfo, null) { CorrelationId = ctx.CorrelationId.Value };

        await ctx.Send(MessagingHelper.GetCommandEndpointAddress<CommitClusterChange>(), command);
    }

    private static Task SendAddClusterNodesCommand<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class => ctx.Send(MessagingHelper.GetCommandEndpointAddress<AddClusterNodesCommand>(),
                            new AddClusterNodesCommand(ctx.Saga.CorrelationId, ctx.Saga.ClusterNodesToCreate));

    private static Task SendCleanClusterNodesCommand<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx, List<Guid> nodesToClean)
        where TMessage : class => ctx.Send(MessagingHelper.GetCommandEndpointAddress<CleanClusterNodesCommand>(),
            new CleanClusterNodesCommand(ctx.Saga.ClusterId, [.. nodesToClean]));

    private Task<UpdateDeployments> CreateUpdateDeploymentsTargetVersion<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class => CreateUpdateDeploymentsEvent(ctx.Saga.ClusterId, ctx.Saga.TargetVersion, ctx.CancellationToken);

    private Task<UpdateDeployments> CreateUpdateDeploymentsActiveVersion<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class
        => CreateUpdateDeploymentsEvent(ctx.Saga.ClusterId, ctx.Saga.ActiveVersion, ctx.CancellationToken);

    private async Task<UpdateDeployments> CreateUpdateDeploymentsEvent(Guid clusterId, Version version, CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var snapshotStore = scope.ServiceProvider.GetRequiredService<IClusterSnapshotStore>();
        var targetCluster = await snapshotStore.LoadCompressedJson(clusterId, version, cancellationToken) ??
            throw new ArgumentException($"Cluster {clusterId} v{version} not found"); //Will throw in IClusterSnapshotStore

        return new(targetCluster, clusterId);
    }
}
