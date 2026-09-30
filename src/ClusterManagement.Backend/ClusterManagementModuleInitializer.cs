using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Messaging;
using Sdk.Backend.Modules;
using Sdk.Connections.Commands;
using Sdk.Instance;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Builder.Abstractions;
using ViciOne.Cluster.Model;
using ViciOne.Core.Dataflow.DataModel;

namespace ClusterManagement.Backend;

public sealed class ClusterManagementModuleInitializer : IModuleInitializer
{
    private const string DotnetRunningInContainer = "DOTNET_RUNNING_IN_CONTAINER";

    private static readonly TimeSpan UploadedPackageMaxAge = TimeSpan.FromMinutes(5);

    public async Task OnPreMigrate(IServiceProvider scopedServices, CancellationToken stoppingToken = default)
    {
        await scopedServices.GetRequiredService<IClusterDependencyStore>().Initialize(stoppingToken);

        scopedServices.GetRequiredService<IPackageStore>().CleanupLeftovers(UploadedPackageMaxAge);
    }

    public async Task Migrate(IServiceProvider scopedServices, CancellationToken stoppingToken = default)
    {
        var clusterDbContext = scopedServices.GetRequiredService<IClusterDbContext>();
        var clusterEditorDbContext = scopedServices.GetRequiredService<IClusterEditorDbContext>();
        var engineDbContext = scopedServices.GetRequiredService<IEngineDbContext>();
        var sagaDbContext = scopedServices.GetRequiredService<IClusterSagaDbContext>();

        await clusterDbContext.MigrateAsync(stoppingToken);
        await clusterEditorDbContext.MigrateAsync(stoppingToken);
        await engineDbContext.MigrateAsync(stoppingToken);
        await sagaDbContext.MigrateAsync(stoppingToken);

        await scopedServices.GetRequiredService<IEngineHostPool>().Migrate(stoppingToken);
    }

    public async Task OnPostMigrate(IServiceProvider scopedServices, CancellationToken stoppingToken = default)
    {
        var clusterDb = scopedServices.GetRequiredService<IClusterDbContext>();
        var store = scopedServices.GetRequiredService<IClusterSnapshotStore>();
        var instanceId = scopedServices.GetRequiredService<IInstanceInformationProvider>().Local;
        await SeedDataBase(clusterDb, instanceId, store, stoppingToken);

        await scopedServices.GetRequiredService<IClusterDependencyStore>()
            .ProcessQueuedOperations(stoppingToken);

        await scopedServices.GetRequiredService<ClusterSnapshotStoreMigration>()
            .Migrate(stoppingToken);
    }

    public Task OnInitialized(IServiceProvider scopedServices, CancellationToken stoppingToken = default)
        => scopedServices.GetRequiredService<ISuiteMediator>()
            .Send(new UpsertTag(Public.Constants.CentralBroker), stoppingToken);

    private static async Task SeedDataBase(IClusterDbContext clusterDb,
        IInstanceInformation localInstanceInfo,
        IClusterSnapshotStore store,
        CancellationToken stoppingToken)
    {
        if (await clusterDb.ActiveClusters.AnyAsync(stoppingToken))
        {
            var clusters = clusterDb.ActiveClusters
                .AsNoTracking()
                .ToArray();

            var missing = clusters.Where(c => !store.Exists(c.Id, c.Version)).ToArray();

            // all good - we have active clusters in db and matching versions in store
            if (missing.Length == 0)
                return;

            // here the cluster is missing in store so it can't be active anymore!
            // todo - log/throw?
            foreach (var c in missing)
            {
                clusterDb.ActiveClusters.Remove(c);
            }
        }

        var defaultCluster = CreateDefaultCluster(localInstanceInfo);
        clusterDb.ActiveClusters.Add(new ActiveClusterInfo()
        {
            Id = defaultCluster.Id,
            Version = defaultCluster.Version,
            Deployed = DateTimeOffset.UtcNow,
            DeployedBy = "suite"
        });

        await clusterDb.SaveChangesAsync(stoppingToken);

        // store same version 0.0.1 to the store - this one can be edited if it's the latest version
        await store.Store(defaultCluster, stoppingToken);
    }

    private static Cluster CreateDefaultCluster(IInstanceInformation instance)
    {
        var isRunningInContainer = Environment.GetEnvironmentVariable(DotnetRunningInContainer) == "true";

        using var builder = new ClusterBuilder(new NotImplementedResolver());
        builder.Editors.Cluster.SetName("Default Cluster");
        builder.Editors.Cluster.SetVersion(Public.Constants.DefaultClusterVersion);

        // node group
        var nodeGroup = builder.Editors.Cluster.AddNodeGroup("Default NodeGroup");
        builder.Editors.NodeGroup.SetDescription(nodeGroup, "Default Cluster Node Group generated by the system");
        builder.Editors.NodeGroup.SetNetworkType(nodeGroup, instance.Type.GetNetworkType());

        // node
        var typeName = isRunningInContainer
            ? ClusterNodeType.Container
            : ClusterNodeType.Physical;

        var node = builder.Editors.NodeGroup.AddNode(nodeGroup, $"{instance.Name ?? string.Empty} ({typeName})");
        builder.Editors.Node.SetDescription(node, instance.Description ?? string.Empty);
        builder.Editors.Node.SetType(node, isRunningInContainer ? ClusterNodeType.Container : ClusterNodeType.Physical);

        var application = builder.Editors.Node.AddApplication(node, instance.Type.GetCoreOsType(), instance.Name ?? string.Empty, instance.Id);
        builder.Editors.Application.SetCoreOsModules(application, instance.GetInternalViciOneCoreOSModules());
        builder.Editors.Application.SetDescription(application, $"{instance.Type.GetCoreOsType()} {instance.Id}");

        return builder.Cluster;
    }

    /// <summary>
    /// because we use no designs to create the default cluster we don't need a real resolver
    /// </summary>
    private class NotImplementedResolver : IDependencyResolver
    {
        public ClusterDependency ResolveDataPortDesignDependency(string designId)
            => throw new NotImplementedException();

        public FunctionBlockDesign ResolveFunctionBlockDesign(Guid designId)
            => throw new NotImplementedException();

        public ClusterDependency ResolveFunctionBlockDesignDependency(Guid designId)
            => throw new NotImplementedException();
    }
}
