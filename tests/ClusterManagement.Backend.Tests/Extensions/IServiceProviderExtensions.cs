using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Tests.Tools;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ViciOne.Cluster.Model;


namespace ClusterManagement.Backend.Tests.Extensions;

internal static class IServiceProviderExtensions
{
    public static (Cluster, Cluster) PrepareDeployClusterSaga(this IServiceProvider services, IClusterSnapshotStore store, Func<IServiceProvider, Cluster> prepareActive, Func<IServiceProvider, Cluster> prepareTarget)
    {
        // active cluster needs to be stored in database
        var dbContext = services.GetRequiredService<IClusterDbContext>();

        var activeCluster = services.PrepareActiveClusterSaga(store, prepareActive);

        // target version needs to be stored in snapshot store
        var targetCluster = prepareTarget.Invoke(services);
        store.Load(targetCluster.Id, targetCluster.Version, Arg.Any<CancellationToken>()).Returns(targetCluster);

        return new(activeCluster, targetCluster);
    }

    public static Cluster PrepareActiveClusterSaga(this IServiceProvider services, IClusterSnapshotStore store, Func<IServiceProvider, Cluster> prepareActive)
    {
        // active cluster needs to be stored in database
        var dbContext = services.GetRequiredService<IClusterDbContext>();

        var activeCluster = prepareActive.Invoke(services);
        dbContext.SaveActiveClusterVersion(activeCluster);
        store.Load(activeCluster.Id, activeCluster.Version, Arg.Any<CancellationToken>()).Returns(activeCluster);

        return activeCluster;
    }
}
