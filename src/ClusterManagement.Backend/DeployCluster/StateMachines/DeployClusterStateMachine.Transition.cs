using System.Globalization;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.DeployCluster.Extensions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Messaging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.DeployCluster.StateMachines;

public sealed partial class DeployClusterStateMachine
{
    private async Task StartDeploymentSaga(BehaviorContext<DeployClusterState, Public.Commands.DeployCluster> ctx)
    {
        LogInit(_logger,
            ctx.Message.CorrelationId,
            ctx.Message.ClusterId,
            ctx.Message.TargetVersion,
            ctx.Message.DeletePreviousVersionOnSuccess,
            ctx.Message.Options is not null);

        try
        {
            ctx.Saga.CorrelationId = ctx.Message.CorrelationId;
            ctx.Saga.ClusterId = ctx.Message.ClusterId;
            ctx.Saga.Started = DateTime.UtcNow;

            using var scope = _services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IClusterDbContext>();
            var snapshotStore = scope.ServiceProvider.GetRequiredService<IClusterSnapshotStore>();

            // Load active cluster version
            var activeVersion = await dbContext.ActiveClusters.FirstOrDefaultAsync(c => c.Id == ctx.Message.ClusterId, ctx.CancellationToken)
                ?? throw new InvalidOperationException($"Could not load active cluster id = {ctx.Message.ClusterId}.");

            ctx.Saga.ActiveVersion = activeVersion.Version;
            ctx.Saga.RemoveVersion = !ctx.Message.DeletePreviousVersionOnSuccess
                ? DeployClusterState.EmptyVersion
                : activeVersion.Version;

            // Target does not exist yet so continue with transition to CommittingClusterChange
            if (ctx.Message.Options is not null)
            {
                var commitVersion = await snapshotStore.GetLatestVersionIncrement(ctx.Saga.ClusterId, ctx.Message.Options.IncrementKind);
                ctx.Saga.CommitVersion = commitVersion;
                return;
            }

            // Target an existing version or use incremented version on commit
            if (ctx.Message.TargetVersion is null)
                throw new InvalidOperationException($"Specify an existing TargetVersion or use DeployOptions to commit a target version for cluster {ctx.Message.ClusterId}.");

            ctx.Saga.TargetVersion = ctx.Message.TargetVersion;

            var activeCluster = await snapshotStore.Load(ctx.Message.ClusterId, activeVersion.Version, ctx.CancellationToken) ??
                throw new ArgumentException($"Active Cluster {ctx.Message.ClusterId} v{activeVersion.Version} not found.");

            var targetCluster = await snapshotStore.Load(ctx.Message.ClusterId, ctx.Message.TargetVersion, ctx.CancellationToken)
                    ?? throw new ArgumentException($"Cluster {ctx.Message.ClusterId} v{ctx.Message.TargetVersion} not found.");

            // If we set the version on commit metainfo it's not set for the cluster
            targetCluster.Version = ctx.Message.TargetVersion;

            await PublishClusterUpdateStarted(ctx, activeCluster, targetCluster);
        }
        catch (Exception ex)
        {
            ctx.Saga.InitializeFailed = true;

            LogInitFailed(_logger, ex, ctx.CorrelationId, ctx.Message.ClusterId);

            // Send failed event here instead of binding action to have access to error details
            var failed = new ClusterUpdateFailed(ctx.Saga.CorrelationId, ctx.Saga.ClusterId, ctx.Saga.TargetVersion)
            {
                Duration = DateTime.UtcNow - ctx.Saga.Started,
                Reason = ex.Message,
                State = nameof(Initial)
            };

            await ctx.Publish(failed, ctx.CancellationToken);
        }
    }

    private async Task ClusterChangeCommitted(BehaviorContext<DeployClusterState, ClusterChanged> ctx)
    {
        LogEvent(ctx, nameof(ClusterChangeCommitted));

        try
        {
            if (ctx.Message.Action != CrudAction.Created)
                throw new InvalidOperationException($"Target cluster version '{ctx.Saga.TargetVersion}' could not be created.");

            if (ctx.Message.Version != ctx.Saga.CommitVersion)
                throw new InvalidOperationException($"Create cluster version '{ctx.Message.Version}' does not match expected target version '{ctx.Saga.CommitVersion}'.");

            // The cluster version was created successfully so it's out target version now
            ctx.Saga.TargetVersion = ctx.Saga.CommitVersion;
            LogCreatedCommitVersion(_logger, ctx.CorrelationId, ctx.Saga.ClusterId, ctx.Saga.CommitVersion);

            using var scope = _services.CreateScope();
            var snapshotStore = scope.ServiceProvider.GetRequiredService<IClusterSnapshotStore>();

            var activeCluster = await snapshotStore.Load(ctx.Message.ClusterId, ctx.Saga.ActiveVersion, ctx.CancellationToken) ??
                throw new ArgumentException($"Active Cluster {ctx.Message.ClusterId} version '{ctx.Saga.ActiveVersion}' not found.");

            var targetCluster = await snapshotStore.Load(ctx.Message.ClusterId, ctx.Saga.TargetVersion, ctx.CancellationToken)
                    ?? throw new ArgumentException($"Target Cluster {ctx.Message.ClusterId} version '{ctx.Saga.TargetVersion}' not found.");

            await PublishClusterUpdateStarted(ctx, activeCluster, targetCluster);
        }
        catch (Exception ex)
        {
            ctx.Saga.InitializeFailed = true;
            LogCreatedCommitVersionFailed(_logger, ex, ctx.Saga.CorrelationId, ctx.Saga.ClusterId, ctx.Saga.CommitVersion);

            // Send failed event here instead of binding action to have access to error details
            var failed = new ClusterUpdateFailed(ctx.Saga.CorrelationId, ctx.Saga.ClusterId, ctx.Saga.TargetVersion)
            {
                Duration = DateTime.UtcNow - ctx.Saga.Started,
                Reason = ex.Message,
                State = nameof(CommittingCluster)
            };

            await ctx.Publish(failed, ctx.CancellationToken);
        }
    }

    private static async Task PublishClusterUpdateStarted<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx, Cluster activeCluster, Cluster targetCluster)
        where TMessage : class
    {
        var activeNodes = GetClusterNodeAndCoreOsApplicationIds(activeCluster);
        var targetNodes = GetClusterNodeAndCoreOsApplicationIds(targetCluster);

        var nodesToCreate = targetNodes
                    .ExceptBy(activeNodes.Select(e => e.Key.Id), k => k.Key.Id)
                    .ToDictionary(x => x.Key.Id, x => x.Value?.Id);

        var nodesToClean = activeNodes.Keys
            .ExceptBy(targetNodes.Keys.Select(e => e.Id), k => k.Id)
            .Select(k => k.Id)
            .ToArray();

        var applicationsToUpdate = targetNodes.Values.Where(v => v is not null).Select(v => v!)
            .Union(activeNodes.Values.Where(v => v is not null).Select(v => v!))
            .DistinctBy(v => v.Id)
            .ToDictionary(k => k.Id, k => k.Name);

        // ensure AppId does not change!
        foreach (var (key, value) in nodesToCreate)
            ctx.Saga.ClusterNodesToCreate.Add(key, value);

        ctx.Saga.ClusterNodesToClean.AddRange(nodesToClean);
        ctx.Saga.ClusterApplicationsToUpdate.AddRange(applicationsToUpdate.Keys);

        // message for ui - maybe trigger requests instead sending this information                
        var startedEvent = new ClusterUpdateStarted(ctx.Saga.CorrelationId, targetCluster.Id, targetCluster.Version)
        {
            NodeApplications = GetNodeApplications(targetNodes, activeNodes),
            NodesToClean = [.. nodesToClean],
            NodesToCreate = [.. nodesToCreate.Keys],
            Nodes = GetNodes(targetNodes, activeNodes),
            Applications = applicationsToUpdate
        };

        await ctx.Publish(startedEvent);

        Dictionary<Guid, Guid?> GetNodeApplications(Dictionary<ClusterNode, ClusterApplication?> targets, Dictionary<ClusterNode, ClusterApplication?> actives)
        {
            var nodeApps = targets.ToDictionary(k => k.Key.Id, v => v.Value?.Id);
            foreach (var node in actives)
                nodeApps.TryAdd(node.Key.Id, node.Value?.Id);
            return nodeApps;
        }

        Dictionary<Guid, string> GetNodes(Dictionary<ClusterNode, ClusterApplication?> targets, Dictionary<ClusterNode, ClusterApplication?> actives)
        {
            var nodes = targets.ToDictionary(k => k.Key.Id, v => v.Key.Name);
            foreach (var node in actives)
                nodes.TryAdd(node.Key.Id, node.Key.Name);
            return nodes;
        }
    }

    private void CommitClusterDeleted(BehaviorContext<DeployClusterState, ClusterChanged> ctx)
    {
        LogEvent(ctx, nameof(CommitClusterDeleted));

        // Our only interest is the remove event of our created version
        if (ctx.Saga.IsCommitVersionEmpty() || ctx.Message.Version != ctx.Saga.CommitVersion)
            return;

        if (ctx.Message.Action != CrudAction.Deleted)
            return;

        LogRemovedCommitCluster(_logger, ctx.CorrelationId, ctx.Saga.CommitVersion);

        ctx.Saga.CommitVersion = DeployClusterState.EmptyVersion;
    }

    private void PreviousClusterDeleted(BehaviorContext<DeployClusterState, ClusterChanged> ctx)
    {
        LogEvent(ctx, nameof(PreviousClusterDeleted));

        // Our only interest is the remove event of our active version
        if (ctx.Message.Version is null)
            return;

        if (ctx.Message.Action != CrudAction.Deleted)
            return;

        LogRemovedPreviousCluster(_logger, ctx.Saga.CorrelationId, ctx.Message.Version);
    }

    private void CommitClusterDeleteFailed(BehaviorContext<DeployClusterState, ClusterError> ctx)
    {
        LogEvent(ctx, nameof(CommitClusterDeleteFailed));

        // Our only interest is the remove of our created version
        if (ctx.Saga.IsCommitVersionEmpty())
            return;

        if (ctx.Message.Error?.ErrorCode != Public.Events.ClusterError.DeleteFailed)
            return;

        LogRemoveCommitClusterFailed(_logger, ctx.Saga.CorrelationId, ctx.Saga.CommitVersion);

        ctx.Saga.CommitVersion = DeployClusterState.EmptyVersion;
    }

    private void PreviousClusterDeleteFailed(BehaviorContext<DeployClusterState, ClusterError> ctx)
    {
        LogEvent(ctx, nameof(PreviousClusterDeleteFailed));

        // Our only interest is the remove of our created version
        if (ctx.Saga.ActiveVersion != ctx.Message.Version)
            return;

        if (ctx.Message.Error?.ErrorCode != Public.Events.ClusterError.DeleteFailed)
            return;

        LogRemovePreviousClusterFailed(_logger, ctx.Saga.CorrelationId, ctx.Saga.ActiveVersion);
    }

    private Task AddingClusterNodesWhenClusterNodeAdded(
        BehaviorContext<DeployClusterState, ClusterNodeAddedEvent> ctx)
    {
        ctx.Saga.ClusterNodesCreated.Add(ctx.Message.ClusterNodeId);
        LogEvent(ctx,
            $"{nameof(ctx.Saga.ClusterNodesCreated)} nodeId:{ctx.Message.ClusterNodeId} nodesCreated:{string.Join(", ", ctx.Saga.ClusterNodesCreated)}");

        var change = new ClusterUpdateChanged(ctx.Saga.CorrelationId, ctx.Saga.ClusterId, ctx.Saga.TargetVersion, GetState(ctx.Saga.CurrentState))
        {
            Change = ClusterUpdateChange.Create,
            NodeId = ctx.Message.ClusterNodeId
        };

        return ctx.Publish(change);
    }

    private Task AddingClusterNodesWhenClusterApplicationStarted(
        BehaviorContext<DeployClusterState, ClusterApplicationStartedEvent> ctx)
    {
        ctx.Saga.ClusterApplicationStarted.Add(ctx.Message.ClusterApplicationId);
        LogEvent(ctx,
            $"{nameof(ctx.Saga.ClusterApplicationStarted)} appId:{ctx.Message.ClusterApplicationId} appsStarted:{string.Join(", ", ctx.Saga.ClusterApplicationStarted)}");

        var change = new ClusterUpdateChanged(ctx.Saga.CorrelationId, ctx.Saga.ClusterId, ctx.Saga.TargetVersion, GetState(ctx.Saga.CurrentState))
        {
            Change = ClusterUpdateChange.Start,
            ApplicationId = ctx.Message.ClusterApplicationId
        };

        return ctx.Publish(change);
    }

    private Task ProcessNodeDeploymentUpdated(
        BehaviorContext<DeployClusterState, ClusterNodeDeploymentsUpdatedEvent> ctx)
    {
        // instanceId -> appId 1:1 currently
        ctx.Saga.ClusterApplicationsUpdateFinished.Add(ctx.Message.ClusterApplicationId, null);

        LogProcessNodeDeploymentUpdated(_logger, ctx.Saga.CorrelationId, ctx.Message.ClusterApplicationId);

        var change = new ClusterUpdateChanged(ctx.Saga.CorrelationId, ctx.Saga.ClusterId, ctx.Saga.TargetVersion, GetState(ctx.Saga.CurrentState))
        {
            Change = ClusterUpdateChange.Update,
            ApplicationId = ctx.Message.ClusterApplicationId
        };

        return ctx.Publish(change);
    }

    private Task ProcessNodeDeploymentFailed(
        BehaviorContext<DeployClusterState, ClusterNodeDeploymentsUpdateFailedEvent> ctx)
    {
        ctx.Saga.AtLeastOneDeployReturnedError = true;
        ctx.Saga.ClusterApplicationsUpdateFinished.Add(ctx.Message.ClusterApplicationId, ctx.Message.ErrorInfo);

        LogProcessNodeDeploymentFailed(_logger, ctx.Saga.CorrelationId, ctx.Message.ClusterApplicationId);

        var change = new ClusterUpdateChanged(ctx.Saga.CorrelationId, ctx.Saga.ClusterId, ctx.Saga.TargetVersion, GetState(ctx.Saga.CurrentState))
        {
            Change = ClusterUpdateChange.Update,
            ApplicationId = ctx.Message.ClusterApplicationId,
            Error = ctx.Message.ErrorInfo
        };

        return ctx.Publish(change);
    }

    private void ProcessClusterNodeCleaned(
        BehaviorContext<DeployClusterState, ClusterNodeCleanedEvent> ctx)
    {
        ctx.Saga.ClusterNodesCleaned.Add(ctx.Message.NodeId);
        LogEvent(ctx,
            $"{nameof(ctx.Saga.ClusterNodesCleaned)} nodeId:{ctx.Message.NodeId} nodesCleaned:{string.Join(", ", ctx.Saga.ClusterNodesCleaned)}");
    }

    private void ProcessRollbackNodeDeploymentUpdated(
        BehaviorContext<DeployClusterState, ClusterNodeDeploymentsUpdatedEvent> ctx)
    {
        ctx.Saga.ClusterApplicationsUpdateFinished.Add(ctx.Message.ClusterApplicationId, null);
        LogEvent(ctx,
            $"{nameof(ctx.Saga.ClusterApplicationsUpdateFinished)} appId:{ctx.Message.ClusterApplicationId} appsFinished:{string.Join(", ", ctx.Saga.ClusterApplicationsUpdateFinished.Keys)}");
    }

    private void ProcessRollbackNodeDeploymentFailed(
        BehaviorContext<DeployClusterState, ClusterNodeDeploymentsUpdateFailedEvent> ctx)
    {
        ctx.Saga.ClusterApplicationsUpdateFinished.Add(ctx.Message.ClusterApplicationId, ctx.Message.ErrorInfo);
        LogEvent(ctx,
            string.Format(CultureInfo.InvariantCulture,
                "{0} appId:{1} appsFinished:{2} with {3} errors",
                nameof(ctx.Saga.ClusterApplicationsUpdateFinished),
                ctx.Message.ClusterApplicationId,
                string.Join(", ", ctx.Saga.ClusterApplicationsUpdateFinished.Keys),
                ctx.Saga.ClusterApplicationsUpdateFinished.Count(k => k.Value is not null)));
    }

    /// <summary>
    /// Set the active cluster version to target version and publish <see cref="ClusterUpdateCompleted"/>
    /// </summary>    
    private async Task CompleteClusterUpdate<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class
    {
        LogDeployDuration(_logger, ctx.Saga.CorrelationId, ctx.Saga.ClusterId, DateTime.UtcNow - ctx.Saga.Started);

        await ctx.Publish(new ClusterUpdateCompleted(ctx.Saga.CorrelationId, ctx.Saga.ClusterId, ctx.Saga.TargetVersion, DateTime.UtcNow - ctx.Saga.Started));
    }

    /// <summary>
    /// Set the active cluster version to target version and publish <see cref="ClusterUpdateCompleted"/>
    /// </summary>    
    private async Task UpdateActiveCluster<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class
    {
        using var scope = _services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IClusterDbContext>();

        await dbContext.UpsertActiveClusterInfo(ctx.Saga.ClusterId, ctx.Saga.TargetVersion, DateTimeOffset.UtcNow, null, ctx.CancellationToken);
    }

    private void BeforeFailedFinalize<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class
    {
        foreach (var item in ctx.Saga.ClusterApplicationsUpdateFinished)
        {
            if (item.Value is null)
            {
                LogApplicationUpdated(_logger, ctx.Saga.CorrelationId, item.Key);
                continue;
            }

            LogApplicationUpdateFailure(_logger, ctx.Saga.CorrelationId, item.Key, item.Value!.Message);
        }

        LogDeployDuration(_logger, ctx.Saga.CorrelationId, ctx.Saga.ClusterId, DateTime.UtcNow - ctx.Saga.Started);
    }
}
