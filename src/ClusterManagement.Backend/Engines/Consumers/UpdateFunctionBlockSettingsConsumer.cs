using System.Text.Json;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Public;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Extensions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Messaging;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Builder.Abstractions;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Engines.Consumers;

public class UpdateFunctionBlockSettingsConsumer(IClusterDbContext clusterDb, IClusterSnapshotStore snapshotStore, IClusterLock clusterLock, ISuiteMediator mediator,
    IDesignProvider designProvider) : IConsumer<UpdateFunctionBlockSettings>
{
    public async Task Consume(ConsumeContext<UpdateFunctionBlockSettings> context)
    {
        var dependencyResolver = await designProvider.CreateResolver();

        Cluster snapshot;

        // Loading through storing is one change: a parallel change of the snapshots starts from this one's.
        // The active version is read under the lock, after the changes that ran before this one.
        using (await clusterLock.Acquire(context.Message.ClusterId, context.CancellationToken))
        {
            var activeCluster = await clusterDb.ActiveClusters.AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == context.Message.ClusterId, context.CancellationToken)
                ?? throw new InvalidOperationException($"Could not load active cluster {context.Message.ClusterId}.");
            snapshot = await LoadSnapshot(activeCluster.Id, activeCluster.Version, context.CancellationToken);
            UpdateSettingsInCluster(context.Message.SettingsToApply, snapshot, dependencyResolver);
            await snapshotStore.Store(snapshot, replace: true, context.CancellationToken);
        }

        await UpdateSettingsInEngines(context.Message.SettingsToApply, snapshot, context.CancellationToken);
    }

    private async Task<Cluster> LoadSnapshot(Guid clusterId, Version clusterVersion, CancellationToken cancellationToken)
        => await snapshotStore.Load(clusterId, clusterVersion, cancellationToken)
            ?? throw new InvalidOperationException($"Could not load cluster snapshot '{clusterId}' {clusterVersion}.");

    private static void UpdateSettingsInCluster(IReadOnlyCollection<FunctionBlockParameterValues> settings, Cluster cluster, IDependencyResolver dependencyResolver)
    {
        using ClusterBuilder builder = new(cluster, dependencyResolver);
        var newSettings = settings.SelectMany(s => s.ParametersValue.Select(p => (Id: p.Key, p.Value)));
        var matchedSettings = builder.Cache.Settings.Join(newSettings, s => s.Id, s => s.Id, (s, ns) => (Setting: s, NewValue: ns.Value));
        foreach (var matchedSetting in matchedSettings)
        {
            var type = Type.GetType(matchedSetting.NewValue.Type) ??
                throw new InvalidOperationException($"Cannot find type '{matchedSetting.NewValue.Type}' for setting '{matchedSetting.Setting.Id}' ({matchedSetting.Setting.Name}).");
            var newValue = matchedSetting.NewValue.Value is not null ? JsonSerializer.Deserialize(matchedSetting.NewValue.Value, type) : default;
            builder.Editors.Setting.SetValue(matchedSetting.Setting, newValue);
        }
    }

    private async Task UpdateSettingsInEngines(IReadOnlyCollection<FunctionBlockParameterValues> settings, Cluster cluster,
        CancellationToken cancellationToken)
    {
        var settingsPerEngines = GroupSettingsPerEngines(settings, cluster);
        await ApplySettingsToEngines(cluster, settingsPerEngines, cancellationToken);
    }

    private static Dictionary<Guid, List<FunctionBlockParameterValues>> GroupSettingsPerEngines(
        IReadOnlyCollection<FunctionBlockParameterValues> settingsChanges, Cluster cluster)
    {
        Dictionary<Guid, List<FunctionBlockParameterValues>> settingsPerEngines = [];
        foreach (var settingChanges in settingsChanges)
        {
            var functionBlock = cluster.Dataflows.SelectMany(d => d.Root.Flatten()).SelectMany(c => c.FunctionBlocks).FirstOrDefault(fb => fb.Id == settingChanges.FunctionBlockId);
            if (functionBlock is not null && functionBlock.EngineId.HasValue)
            {
                if (!settingsPerEngines.TryGetValue(functionBlock.EngineId.Value, out var settingsPerEngine))
                {
                    settingsPerEngine = [];
                    settingsPerEngines.Add(functionBlock.EngineId.Value, settingsPerEngine);
                }
                settingsPerEngine.Add(settingChanges);
            }
        }
        return settingsPerEngines;
    }

    private async Task ApplySettingsToEngines(Cluster cluster, IReadOnlyDictionary<Guid, List<FunctionBlockParameterValues>> settingsPerEngines,
        CancellationToken cancellationToken)
    {
        List<Task> tasks = [];
        var allApplications = cluster.NodeGroups.SelectMany(g => g.Nodes).SelectMany(n => n.Applications).ToArray();
        foreach (var settingsPerEngine in settingsPerEngines)
        {
            foreach (var application in allApplications)
            {
                var engine = application.EngineHosts.SelectMany(h => h.Engines)
                    .FirstOrDefault(e => e.Id == settingsPerEngine.Key);
                if (engine is not null && engine.Enabled)
                {
                    var applySettingsCommand = new ApplyFunctionBlockSettings(engine.Id, settingsPerEngine.Value);
                    var task = mediator.Send(applySettingsCommand, application.Id, cancellationToken);
                    tasks.Add(task);
                }
            }
        }
        await Task.WhenAll(tasks);
    }
}
