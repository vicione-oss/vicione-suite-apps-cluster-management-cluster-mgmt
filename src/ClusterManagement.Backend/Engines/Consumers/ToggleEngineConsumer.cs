using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Public.Commands;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;
using Sdk.Messaging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Engines.Consumers;

public sealed class ToggleEngineConsumer(IClusterDbContext database, IClusterSnapshotStore snapshotStore, IClusterLock clusterLock, ISuiteMediator mediator) :
    IConsumer<ToggleEngine>
{
    public async Task Consume(ConsumeContext<ToggleEngine> context)
        => await ChangeEngineState(context.Message.EngineId, context.Message.Start, new StartEngineOnInstance(context.Message.EngineId),
            context.CancellationToken);

    public async Task ChangeEngineState<T>(Guid engineId, bool enabled, T messageForInstance, CancellationToken cancellationToken)
        where T : class, IInstanceDependentCommand
    {
        var clusterId = await LoadActiveClusterId(cancellationToken);

        Cluster snapshot;

        // Loading through storing is one change: a parallel change of the snapshots starts from this one's.
        // The active version is read again under the lock, after the changes that ran before this one.
        using (await clusterLock.Acquire(clusterId, cancellationToken))
        {
            var activeCluster = await LoadActiveCluster(clusterId, cancellationToken);
            snapshot = await LoadSnapshot(activeCluster.Id, activeCluster.Version, cancellationToken);
            FindEngine(snapshot, engineId).Enabled = enabled;
            await snapshotStore.Store(snapshot, replace: true, cancellationToken);
        }

        await ChangeEngineStateOnInstance(engineId, messageForInstance, snapshot, cancellationToken);
    }

    private async Task<Guid> LoadActiveClusterId(CancellationToken cancellationToken)
        => (await database.ActiveClusters.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Could not load active cluster.")).Id;

    private async Task<ActiveClusterInfo> LoadActiveCluster(Guid clusterId, CancellationToken cancellationToken)
        => await database.ActiveClusters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clusterId, cancellationToken)
            ?? throw new InvalidOperationException($"Could not load active cluster {clusterId}.");

    private async Task<Cluster> LoadSnapshot(Guid clusterId, Version clusterVersion, CancellationToken cancellationToken)
        => await snapshotStore.Load(clusterId, clusterVersion, cancellationToken)
            ?? throw new InvalidOperationException($"Could not load cluster snapshot '{clusterId}' {clusterVersion}.");

    private static ViciOne.Cluster.Model.Engine FindEngine(Cluster cluster, Guid engineId)
        => cluster.NodeGroups
            .SelectMany(x => x.Nodes)
            .SelectMany(x => x.Applications)
            .SelectMany(x => x.EngineHosts)
            .SelectMany(x => x.Engines)
            .FirstOrDefault(x => x.Id == engineId)
            ?? throw new InvalidOperationException($"Could not find engine '{engineId}' in cluster '{cluster.Id}'.");

    private async Task ChangeEngineStateOnInstance<T>(Guid engineId, T messageForInstance, Cluster cluster, CancellationToken cancellationToken)
        where T : class, IInstanceDependentCommand
    {
        var instanceId = FindInstanceOfEngine(cluster, engineId);
        await mediator.Send(messageForInstance, instanceId, cancellationToken);
    }

    internal static Guid FindInstanceOfEngine(Cluster cluster, Guid engineId)
        => cluster.NodeGroups
            .SelectMany(x => x.Nodes)
            .SelectMany(x => x.Applications)
            .FirstOrDefault(a => a.EngineHosts.SelectMany(eh => eh.Engines).Any(eh => eh.Id == engineId))?.Id
            ?? throw new InvalidOperationException($"Could not find instance running engine '{engineId}' in cluster '{cluster.Id}' {cluster.Version}.");
}
