using System.Globalization;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.DeployCluster.StateMachines;

public sealed partial class DeployClusterStateMachine
{
    private async Task StartDeploymentSaga(BehaviorContext<DeployClusterState, Public.Commands.DeployCluster> ctx)
    {
        LogEvent(ctx, "Initialize saga");

        // Should not happen - ensure we don't overwrite an existing saga
        if (ctx.Saga.ClusterId == ctx.Message.ClusterId)
        {
            await PublishDeployClusterRejected(ctx);
            return;
        }

        try
        {
            ctx.Saga.CorrelationId = ctx.Message.CorrelationId;
            ctx.Saga.ClusterId = ctx.Message.ClusterId;
            ctx.Saga.TargetVersion = ctx.Message.TargetVersion;
            ctx.Saga.Started = DateTime.UtcNow;

            using var scope = _services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IClusterDbContext>();
            var snapshotStore = scope.ServiceProvider.GetRequiredService<IClusterSnapshotStore>();

            // load active cluster
            var activeVersion = await dbContext.ActiveClusters.FirstOrDefaultAsync(c => c.Id == ctx.Message.ClusterId, ctx.CancellationToken)
                ?? throw new InvalidOperationException($"Could not load active cluster id = {ctx.Message.ClusterId}.");

            ctx.Saga.ActiveVersion = activeVersion.Version;

            // Target does not exist yet so continue with transistion to CommittingClusterChange
            if (ctx.Message.Options is not null)
                return;

            var activeCluster = await snapshotStore.Load(ctx.Message.ClusterId, activeVersion.Version, ctx.CancellationToken) ??
                throw new ArgumentException($"Active Cluster {ctx.Message.ClusterId} v{activeVersion.Version} not found");


            var targetCluster = await snapshotStore.Load(ctx.Message.ClusterId, ctx.Message.TargetVersion, ctx.CancellationToken)
                    ?? throw new ArgumentException($"Cluster {ctx.Message.ClusterId} v{ctx.Message.TargetVersion} not found");

            await PublishClusterUpdateStarted(ctx, activeCluster, targetCluster);
        }
        catch (Exception ex)
        {
            ctx.Saga.InitializeFailed = true;
            _logger.LogError(ex, "Failed to initialize deployment saga");

            // Send failed event here instead of binding action to have access to error details
            var failed = new ClusterUpdateFailed(ctx.Saga.ClusterId, ctx.Saga.TargetVersion)
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
                throw new InvalidOperationException($"Target cluster version '{ctx.Saga.TargetVersion}' could not be created");

            if (ctx.Message.Version != ctx.Saga.TargetVersion)
                throw new InvalidOperationException($"Create cluster version '{ctx.Message.Version}' does not match expected target version '{ctx.Saga.TargetVersion}'");

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
            _logger.LogError(ex, "Failed to init deployment with cluster {Id} active version '{Active}' to target version {Target}",
                ctx.Saga.ClusterId, ctx.Saga.ActiveVersion, ctx.Saga.TargetVersion);

            // Send failed event here instead of binding action to have access to error details
            var failed = new ClusterUpdateFailed(ctx.Saga.ClusterId, ctx.Saga.TargetVersion)
            {
                Duration = DateTime.UtcNow - ctx.Saga.Started,
                Reason = ex.Message,
                State = nameof(CommittingCluster)
            };

            await ctx.Publish(failed, ctx.CancellationToken);
        }
    }

    private async Task PublishClusterUpdateStarted<TMessage>(BehaviorContext<DeployClusterState, TMessage> context, Cluster activeCluster, Cluster targetCluster)
        where TMessage : class
    {
        LogEvent(context, $"Prepare transition from cluster version {activeCluster.Version} to {targetCluster.Version}");

        var activeNodes = GetClusterNodeAndCoreOsApplicationIds(activeCluster);
        var targetNodes = GetClusterNodeAndCoreOsApplicationIds(targetCluster);

        var nodesToCreate = targetNodes
                    .ExceptBy(activeNodes.Select(e => e.Key.Id), k => k.Key.Id)
                    .ToDictionary(x => x.Key.Id, x => x.Value?.Id);

        var nodesToClean = activeNodes.Keys
            .ExceptBy(targetNodes.Keys.Select(e => e.Id), k => k.Id)
            .Select(k => k.Id);

        var applicationsToUpdate = targetNodes.Values.Where(v => v is not null).Select(v => v!)
            .Union(activeNodes.Values.Where(v => v is not null).Select(v => v!))
            .DistinctBy(v => v.Id)
            .ToDictionary(k => k.Id, k => k.Name);

        // ensure AppId does not change!
        foreach (var (key, value) in nodesToCreate)
            context.Saga.ClusterNodesToCreate.Add(key, value);

        context.Saga.ClusterNodesToClean.AddRange(nodesToClean);
        context.Saga.ClusterApplicationsToUpdate.AddRange(applicationsToUpdate.Keys);

        // message for ui - maybe trigger requests instead sending this information                
        var startedEvent = new ClusterUpdateStarted(targetCluster.Id, targetCluster.Version)
        {
            NodeApplications = GetNodeApplications(targetNodes, activeNodes),
            NodesToClean = [.. nodesToClean],
            NodesToCreate = [.. nodesToCreate.Keys],
            Nodes = GetNodes(targetNodes, activeNodes),
            Applications = applicationsToUpdate
        };

        await context.Publish(startedEvent);

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

    private Task AddingClusterNodesWhenClusterNodeAdded(
        BehaviorContext<DeployClusterState, ClusterNodeAddedEvent> context)
    {
        context.Saga.ClusterNodesCreated.Add(context.Message.ClusterNodeId);
        LogEvent(context,
            $"{nameof(context.Saga.ClusterNodesCreated)} nodeId:{context.Message.ClusterNodeId} nodesCreated:{string.Join(", ", context.Saga.ClusterNodesCreated)}");

        var change = new ClusterUpdateChanged(context.Saga.ClusterId, context.Saga.TargetVersion, GetState(context.Saga.CurrentState))
        {
            Change = ClusterUpdateChange.Create,
            NodeId = context.Message.ClusterNodeId
        };

        return context.Publish(change);
    }

    private Task AddingClusterNodesWhenClusterApplicationStarted(
        BehaviorContext<DeployClusterState, ClusterApplicationStartedEvent> context)
    {
        context.Saga.ClusterApplicationStarted.Add(context.Message.ClusterApplicationId);
        LogEvent(context,
            $"{nameof(context.Saga.ClusterApplicationStarted)} appId:{context.Message.ClusterApplicationId} appsStarted:{string.Join(", ", context.Saga.ClusterApplicationStarted)}");

        var change = new ClusterUpdateChanged(context.Saga.ClusterId, context.Saga.TargetVersion, GetState(context.Saga.CurrentState))
        {
            Change = ClusterUpdateChange.Start,
            ApplicationId = context.Message.ClusterApplicationId
        };

        return context.Publish(change);
    }

    private Task ProcessNodeDeploymentUpdated(
        BehaviorContext<DeployClusterState, ClusterNodeDeploymentsUpdatedEvent> context)
    {
        // instanceId -> appId 1:1 currently
        context.Saga.ClusterApplicationsUpdateFinished.Add(context.Message.ClusterApplicationId, null);
        LogEvent(context,
            $"{nameof(context.Saga.ClusterApplicationsUpdateFinished)} appId:{context.Message.ClusterApplicationId} appsFinished:{string.Join(", ", context.Saga.ClusterApplicationsUpdateFinished.Keys)}");

        var change = new ClusterUpdateChanged(context.Saga.ClusterId, context.Saga.TargetVersion, GetState(context.Saga.CurrentState))
        {
            Change = ClusterUpdateChange.Update,
            ApplicationId = context.Message.ClusterApplicationId
        };

        return context.Publish(change);
    }

    private Task ProcessNodeDeploymentFailed(
        BehaviorContext<DeployClusterState, ClusterNodeDeploymentsUpdateFailedEvent> context)
    {
        context.Saga.AtLeastOneDeployReturnedError = true;
        context.Saga.ClusterApplicationsUpdateFinished.Add(context.Message.ClusterApplicationId, context.Message.ErrorInfo);
        LogEvent(context,
            $"{nameof(context.Saga.ClusterApplicationsUpdateFinished)} on appId: {context.Message.ClusterApplicationId} appsFinished:{string.Join(", ", context.Saga.ClusterApplicationsUpdateFinished.Keys)} with error");

        var change = new ClusterUpdateChanged(context.Saga.ClusterId, context.Saga.TargetVersion, GetState(context.Saga.CurrentState))
        {
            Change = ClusterUpdateChange.Update,
            ApplicationId = context.Message.ClusterApplicationId,
            Error = context.Message.ErrorInfo
        };

        return context.Publish(change);
    }

    private void ProcessClusterNodeCleaned(
        BehaviorContext<DeployClusterState, ClusterNodeCleanedEvent> context)
    {
        context.Saga.ClusterNodesCleaned.Add(context.Message.NodeId);
        LogEvent(context,
            $"{nameof(context.Saga.ClusterNodesCleaned)} nodeId:{context.Message.NodeId} nodesCleaned:{string.Join(", ", context.Saga.ClusterNodesCleaned)}");
    }

    private void ProcessRollbackNodeDeploymentUpdated(
        BehaviorContext<DeployClusterState, ClusterNodeDeploymentsUpdatedEvent> context)
    {
        context.Saga.ClusterApplicationsUpdateFinished.Add(context.Message.ClusterApplicationId, null);
        LogEvent(context,
            $"{nameof(context.Saga.ClusterApplicationsUpdateFinished)} appId:{context.Message.ClusterApplicationId} appsFinished:{string.Join(", ", context.Saga.ClusterApplicationsUpdateFinished.Keys)}");
    }

    private void ProcessRollbackNodeDeploymentFailed(
        BehaviorContext<DeployClusterState, ClusterNodeDeploymentsUpdateFailedEvent> context)
    {
        context.Saga.ClusterApplicationsUpdateFinished.Add(context.Message.ClusterApplicationId, context.Message.ErrorInfo);
        LogEvent(context,
            string.Format(CultureInfo.InvariantCulture,
                "{0} appId:{1} appsFinished:{2} with {3} errors",
                nameof(context.Saga.ClusterApplicationsUpdateFinished),
                context.Message.ClusterApplicationId,
                string.Join(", ", context.Saga.ClusterApplicationsUpdateFinished.Keys),
                context.Saga.ClusterApplicationsUpdateFinished.Count(k => k.Value is not null)));
    }

    /// <summary>
    /// Set the active cluster version to target version and publish <see cref="ClusterUpdateCompleted"/>
    /// </summary>    
    private async Task CompleteClusterUpdate<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class
    {
        LogDeployDuration(ctx.Saga);

        using var scope = _services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IClusterDbContext>();

        await dbContext.UpsertActiveClusterInfo(ctx.Saga.ClusterId, ctx.Saga.TargetVersion, DateTimeOffset.UtcNow, null, ctx.CancellationToken);

        await ctx.Publish(new ClusterUpdateCompleted(ctx.Saga.ClusterId, ctx.Saga.TargetVersion, DateTime.UtcNow - ctx.Saga.Started));
    }

    private void BeforeFailedFinalize<TMessage>(BehaviorContext<DeployClusterState, TMessage> ctx)
        where TMessage : class
    {
        LogDeployDuration(ctx.Saga);

        foreach (var item in ctx.Saga.ClusterApplicationsUpdateFinished.Where(k => k.Value is not null))
        {
            _logger.LogError("Update application = {Id} failed with {Error}.",
                item.Key,
                item.Value!.Message);
        }

        var updatedWithoutErrors = ctx.Saga.ClusterApplicationsUpdateFinished
            .Where(k => k.Value is null)
            .Select(k => k.Key);

        _logger.LogInformation("Updated applications {Ids} without errors.", string.Join(",", updatedWithoutErrors));
    }
}
