using ClusterManagement.Backend.DbContext;
using ClusterManagement.Public.Designs;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests;

internal sealed partial class ClusterSystemTestContext
{
    private ClusterBuilder _clusterBuilder = default!;
    private Cluster _cluster = new();
    private ClusterNode _clusterNode = new();
    private ClusterApplication _defaultClusterApplication = new();

    private async Task LoadCluster()
    {
        var initializer = new ClusterManagementModuleInitializer();
        using var scope = Services.CreateScope();
        await initializer.Migrate(scope.ServiceProvider);
        await initializer.OnPostMigrate(scope.ServiceProvider);

        var dbContext = scope.ServiceProvider.GetRequiredService<IClusterDbContext>();
        var active = await dbContext.ActiveClusters.FirstAsync();

        _cluster = await _snapshotStore.Load(active.Id, active.Version) ?? throw new InvalidOperationException("Failed to load default cluster");
        var designProvider = Services.GetRequiredService<IDesignProvider>();
        _clusterBuilder = new(_cluster, await designProvider.CreateResolver());
        _clusterNode = _clusterBuilder.Cache.ClusterNodes.Single();
        _defaultClusterApplication = _clusterNode.Applications.FirstOrDefault()
            ?? throw new InvalidOperationException("Could not get expected default (seeded) cluster application.");
        _clusterNode.Applications.Remove(_defaultClusterApplication);
    }

    internal void Given_user_added_cluster_application()
        => _clusterNode.Applications.Add(_defaultClusterApplication);

    /* Ob eine Cluster Application wirklich existiert, kann eine Suite Instanz selbst nicht prüfen.
    * Dies muss ein übergeordneter Koordinator prüfen z.B. ein Master über Kubernetes.
    */
    internal void Then_cluster_application_exists()
        => _cluster.NodeGroups.Should().ContainSingle().Which.Nodes.Should().ContainSingle().Which.Applications.Should().Contain(_defaultClusterApplication);

    /* Ob eine Cluster Application wirklich abgebaut wurde, kann eine Suite Instanz selbst nicht prüfen.
        * Dies muss ein übergeordneter Koordinator prüfen z.B. ein Master über Kubernetes.
        */
    internal void Then_no_cluster_application_exists()
        => _cluster.NodeGroups.Should().ContainSingle().Which.Nodes.Should().ContainSingle().Which.Applications.Should().BeEmpty();
}
