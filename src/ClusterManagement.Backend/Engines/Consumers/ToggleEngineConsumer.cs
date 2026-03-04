using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Public.Commands;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;
using Sdk.Messaging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Engines.Consumers;

public sealed class ToggleEngineConsumer(IClusterDbContext database, IClusterSnapshotStore snapshotStore, ISuiteMediator mediator) :
    IConsumer<ToggleEngine>
{
    public async Task Consume(ConsumeContext<ToggleEngine> context)
        => await ChangeEngineState(context.Message.EngineId, context.Message.Start, new StartEngineOnInstance(context.Message.EngineId),
            context.CancellationToken);

    public async Task ChangeEngineState<T>(Guid engineId, bool enabled, T messageForInstance, CancellationToken cancellationToken)
        where T : class, IInstanceDependentCommand
    {
        var cluster = await LoadCluster(cancellationToken);
        var snapshot = await LoadSnapshot(cluster.Id, cluster.Version, cancellationToken);
        SetEngineState(cluster, snapshot, engineId, enabled);
        await Task.WhenAll(
            SaveCluster(cancellationToken),
            SaveSnapshot(snapshot, cancellationToken));
        await ChangeEngineStateOnInstance(engineId, messageForInstance, cluster, cancellationToken);
    }

    private async Task<Cluster> LoadCluster(CancellationToken cancellationToken)
    {
        var activeCluster = await database.ActiveClusters.FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Could not load active cluster.");

        return await LoadSnapshot(activeCluster.Id, activeCluster.Version, cancellationToken);
    }

    private async Task<Cluster> LoadSnapshot(Guid clusterId, Version clusterVersion, CancellationToken cancellationToken)
        => await snapshotStore.Load(clusterId, clusterVersion, cancellationToken)
            ?? throw new InvalidOperationException($"Could not load cluster snapshot '{clusterId}' {clusterVersion}.");

    private static void SetEngineState(Cluster cluster, Cluster snapshot, Guid engineId, bool enabled)
    {
        var engine = FindEngine(cluster, engineId);
        engine.Enabled = enabled;
        engine = FindEngine(snapshot, engineId);
        engine.Enabled = enabled;
    }

    private static ViciOne.Cluster.Model.Engine FindEngine(Cluster cluster, Guid engineId)
        => cluster.NodeGroups
            .SelectMany(x => x.Nodes)
            .SelectMany(x => x.Applications)
            .SelectMany(x => x.EngineHosts)
            .SelectMany(x => x.Engines)
            .FirstOrDefault(x => x.Id == engineId)
            ?? throw new InvalidOperationException($"Could not find engine '{engineId}' in cluster '{cluster.Id}'.");

    private Task<int> SaveCluster(CancellationToken cancellationToken)
        => database.SaveChangesAsync(cancellationToken);

    private async Task SaveSnapshot(Cluster snapshot, CancellationToken cancellationToken)
    {
        snapshotStore.Delete(snapshot.Id, snapshot.Version);
        await snapshotStore.Store(snapshot, cancellationToken);
    }

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
