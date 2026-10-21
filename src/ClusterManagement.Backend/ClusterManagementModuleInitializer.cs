using ClusterManagement.Backend.Connections;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Iodds;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    private static readonly TimeSpan s_uploadedFileMaxAge = TimeSpan.FromMinutes(5);

    public async Task OnPreMigrate(IServiceProvider scopedServices, CancellationToken stoppingToken = default)
    {
        await scopedServices.GetRequiredService<IClusterDependencyStore>().Initialize(stoppingToken);

        scopedServices.GetRequiredService<IPackageStore>().CleanupLeftovers(s_uploadedFileMaxAge);
        scopedServices.GetRequiredService<IoddUploadCleanup>().CleanupLeftovers(s_uploadedFileMaxAge);
        scopedServices.GetRequiredService<IOpcUaCertificateStore>().CleanupUploadLeftovers(s_uploadedFileMaxAge);
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
        var localInstance = scopedServices.GetRequiredService<IInstanceInformationProvider>().Local;
        var logger = scopedServices.GetRequiredService<ILogger<ClusterManagementModuleInitializer>>();
        await SeedDataBase(clusterDb, localInstance, store, logger, stoppingToken);

        await scopedServices.GetRequiredService<IClusterDependencyStore>()
            .ProcessQueuedOperations(stoppingToken);

        await scopedServices.GetRequiredService<ClusterSnapshotStoreMigration>()
            .Migrate(stoppingToken);
    }

    public Task OnInitialized(IServiceProvider scopedServices, CancellationToken stoppingToken = default)
        => scopedServices.GetRequiredService<ISuiteMediator>()
            .Send(new UpsertTag(Public.Constants.CentralBroker), stoppingToken);

    private static async Task SeedDataBase(IClusterDbContext clusterDb,
        IInstanceInformation localInstance,
        IClusterSnapshotStore store,
        ILogger logger,
        CancellationToken stoppingToken)
    {
        var activeClusters = await clusterDb.ActiveClusters.ToListAsync(stoppingToken);
        var keptCount = 0;

        foreach (var active in activeClusters)
        {
            if (store.Exists(active.Id, active.Version)
                || await TryRecoverMissingActiveCluster(active, localInstance, store, logger, stoppingToken))
            {
                keptCount++;
                continue;
            }

            clusterDb.ActiveClusters.Remove(active);
        }

        if (keptCount > 0)
        {
            await clusterDb.SaveChangesAsync(stoppingToken);
            return;
        }

        var defaultCluster = CreateDefaultCluster(localInstance);
        clusterDb.ActiveClusters.Add(new ActiveClusterInfo()
        {
            Id = defaultCluster.Id,
            Version = defaultCluster.Version,
            Deployed = DateTimeOffset.UtcNow,
            DeployedBy = ActiveClusterInfo.DeployedBySuite
        });

        await clusterDb.SaveChangesAsync(stoppingToken);

        // store the default version as well - it can be edited as long as it is the latest version
        await store.Store(defaultCluster, stoppingToken);
    }

    /// <summary>
    /// The snapshot of the active version is missing: activate the newest stored version below it,
    /// or else the stored default version. Newer versions may be drafts that were never deployed,
    /// so the default version is recreated when neither is stored.
    /// </summary>
    private static async Task<bool> TryRecoverMissingActiveCluster(ActiveClusterInfo active,
        IInstanceInformation localInstance,
        IClusterSnapshotStore store,
        ILogger logger,
        CancellationToken stoppingToken)
    {
        var storedVersions = (await store.GetClusterInfos(active.Id, stoppingToken)).ToArray();
        var missingVersion = active.Version;

        // check the default version by its snapshot - a version with an unreadable info file is not in storedVersions
        var replacementVersion = storedVersions
            .Where(i => i.Version < missingVersion)
            .MaxBy(i => i.Version)?.Version
            ?? (store.Exists(active.Id, Public.Constants.DefaultClusterVersion) ? Public.Constants.DefaultClusterVersion : null);

        if (replacementVersion is null && storedVersions.Length == 0)
        {
            logger.LogActiveClusterRemoved(active.Id, missingVersion);
            return false;
        }

        if (replacementVersion is not null)
        {
            active.Version = replacementVersion;
            logger.LogActiveClusterVersionReplaced(active.Id, missingVersion, active.Version);
        }
        else
        {
            var defaultCluster = CreateDefaultCluster(localInstance);
            defaultCluster.Id = active.Id;
            defaultCluster.Name = storedVersions.MinBy(i => i.Version)!.Name;
            await store.Store(defaultCluster, stoppingToken);

            active.Version = defaultCluster.Version;
            logger.LogActiveClusterDefaultVersionRecreated(active.Id, missingVersion, active.Version);
        }

        active.Deployed = DateTimeOffset.UtcNow;
        active.DeployedBy = ActiveClusterInfo.DeployedBySuite;
        return true;
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
