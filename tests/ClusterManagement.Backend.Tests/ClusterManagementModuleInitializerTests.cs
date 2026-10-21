using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Backend.Snapshots.Extensions;
using ClusterManagement.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sdk.Backend.IO;
using Sdk.Backend.Modules;
using Sdk.Instance;
using Sdk.Testing.Backend;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests;

public class ClusterManagementModuleInitializer_OnPostMigrate : TestWithDbContextSqlite<ClusterDbContextSqlite>
{
    private static readonly Version s_defaultVersion = Public.Constants.DefaultClusterVersion;

    private readonly MockFileSystem _fileSystem = new();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
    private readonly IServiceProvider _services;
    private readonly IClusterSnapshotStore _store;
    private readonly DateTimeOffset _deployedBefore = DateTimeOffset.UtcNow.AddDays(-1);

    public ClusterManagementModuleInitializer_OnPostMigrate()
    {
        _workspaceProvider.Home.Returns("home");

        var instanceInformation = Substitute.For<IInstanceInformation>();
        instanceInformation.Name.Returns("Test");
        instanceInformation.Id.Returns(Guid.NewGuid());
        instanceInformation.Type.Returns(InstanceType.Standalone);
        var instanceInformationProvider = Substitute.For<IInstanceInformationProvider>();
        instanceInformationProvider.Local.Returns(instanceInformation);

        _services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(_workspaceProvider)
            .AddSingleton<IFileSystem>(_fileSystem)
            .AddSingleton(Substitute.For<IOptions<ClusterManagementOptions>>())
            .AddSingleton<IAtomicFileWriter, TestAtomicFileWriter>()
            .AddSingleton<IClusterLock, ClusterLock>()
            .AddSingleton<IClusterSnapshotStore, ClusterSnapshotStore>()
            .AddSingleton(Substitute.For<IClusterDependencyStore>())
            .AddSingleton<ClusterSnapshotStoreMigration>()
            .AddSingleton(instanceInformationProvider)
            .AddSingleton<IClusterDbContext>(_ => TestDbContext)
            .BuildServiceProvider();

        _store = _services.GetRequiredService<IClusterSnapshotStore>();
    }

    [Fact]
    public async Task Seeds_the_default_cluster_when_nothing_exists()
    {
        await When_starting_up();

        var active = await ActiveClusters();
        active.Should().ContainSingle().Which.Version.Should().Be(s_defaultVersion);
        (await StoredVersions(active[0].Id)).Should().Equal(s_defaultVersion);
    }

    [Fact]
    public async Task Changes_nothing_when_the_active_snapshot_exists()
    {
        var clusterId = Guid.NewGuid();
        await Given_stored_versions(clusterId, s_defaultVersion, new(0, 2, 0));
        await Given_active(clusterId, new(0, 2, 0));

        await When_starting_up();

        var active = (await ActiveClusters()).Should().ContainSingle().Subject;
        active.Id.Should().Be(clusterId);
        active.Version.Should().Be(new Version(0, 2, 0));
        active.Deployed.Should().Be(_deployedBefore);
        (await StoredClusterIds()).Should().Equal(clusterId);
    }

    [Fact]
    public async Task Activates_the_newest_lower_version_of_the_same_cluster_when_the_active_snapshot_is_missing()
    {
        var clusterId = Guid.NewGuid();
        await Given_stored_versions(clusterId, s_defaultVersion, new(0, 2, 0), new(0, 4, 0));
        await Given_active(clusterId, new(0, 3, 0));

        await When_starting_up();

        var active = (await ActiveClusters()).Should().ContainSingle().Subject;
        active.Id.Should().Be(clusterId);
        active.Version.Should().Be(new Version(0, 2, 0));
        active.Deployed.Should().BeAfter(_deployedBefore);
        active.DeployedBy.Should().Be(ActiveClusterInfo.DeployedBySuite);
        (await StoredClusterIds()).Should().Equal(clusterId);
    }

    [Fact]
    public async Task Activates_the_default_version_when_it_is_the_only_lower_version()
    {
        var clusterId = Guid.NewGuid();
        await Given_stored_versions(clusterId, s_defaultVersion, new(0, 5, 0));
        await Given_active(clusterId, new(0, 4, 0));

        await When_starting_up();

        var active = (await ActiveClusters()).Should().ContainSingle().Subject;
        active.Id.Should().Be(clusterId);
        active.Version.Should().Be(s_defaultVersion);
        (await StoredClusterIds()).Should().Equal(clusterId);
    }

    [Fact]
    public async Task Recreates_the_default_version_of_the_same_cluster_when_only_newer_versions_are_stored()
    {
        var clusterId = Guid.NewGuid();
        await Given_stored_versions(clusterId, "Line 1", new Version(0, 2, 0));
        await Given_active(clusterId, s_defaultVersion);

        await When_starting_up();

        var active = (await ActiveClusters()).Should().ContainSingle().Subject;
        active.Id.Should().Be(clusterId);
        active.Version.Should().Be(s_defaultVersion);
        (await StoredClusterIds()).Should().Equal(clusterId);
        (await StoredVersions(clusterId)).Should().Equal(s_defaultVersion, new(0, 2, 0));
        var recreated = await _store.Load(clusterId, s_defaultVersion, TestContext.Current.CancellationToken);
        recreated!.Name.Should().Be("Line 1");
        recreated.NodeGroups.Should().ContainSingle();
    }

    [Fact]
    public async Task Activates_the_stored_default_version_when_the_missing_version_is_below_it()
    {
        var clusterId = Guid.NewGuid();
        await Given_stored_versions(clusterId, s_defaultVersion, new(0, 2, 0));
        await Given_active(clusterId, new(0, 0, 1));

        await When_starting_up();

        var active = (await ActiveClusters()).Should().ContainSingle().Subject;
        active.Id.Should().Be(clusterId);
        active.Version.Should().Be(s_defaultVersion);
        var stored = await _store.Load(clusterId, s_defaultVersion, TestContext.Current.CancellationToken);
        stored!.NodeGroups.Should().BeEmpty("the stored default version must not be overwritten");
    }

    [Fact]
    public async Task Activates_the_stored_default_version_when_its_info_file_is_missing()
    {
        var clusterId = Guid.NewGuid();
        await Given_stored_versions(clusterId, s_defaultVersion, new(0, 3, 0));
        Given_info_file_is_missing(clusterId, s_defaultVersion);
        await Given_active(clusterId, new(0, 2, 0));

        await When_starting_up();

        var active = (await ActiveClusters()).Should().ContainSingle().Subject;
        active.Id.Should().Be(clusterId);
        active.Version.Should().Be(s_defaultVersion);
        var stored = await _store.Load(clusterId, s_defaultVersion, TestContext.Current.CancellationToken);
        stored!.NodeGroups.Should().BeEmpty("the stored default version must not be overwritten");
    }

    [Fact]
    public async Task Keeps_the_cluster_when_only_the_default_version_without_info_file_is_stored()
    {
        var clusterId = Guid.NewGuid();
        await Given_stored_versions(clusterId, s_defaultVersion);
        Given_info_file_is_missing(clusterId, s_defaultVersion);
        await Given_active(clusterId, new(0, 2, 0));

        await When_starting_up();

        var active = (await ActiveClusters()).Should().ContainSingle().Subject;
        active.Id.Should().Be(clusterId);
        active.Version.Should().Be(s_defaultVersion);
    }

    [Fact]
    public async Task Seeds_the_default_cluster_when_no_version_of_the_active_cluster_is_stored()
    {
        var clusterId = Guid.NewGuid();
        await Given_active(clusterId, new(0, 2, 0));

        await When_starting_up();

        var active = (await ActiveClusters()).Should().ContainSingle().Subject;
        active.Id.Should().NotBe(clusterId);
        active.Version.Should().Be(s_defaultVersion);
        (await StoredClusterIds()).Should().Equal(active.Id);
    }

    [Fact]
    public async Task Does_not_seed_the_default_cluster_while_another_cluster_stays_active()
    {
        var keptId = Guid.NewGuid();
        var lostId = Guid.NewGuid();
        await Given_stored_versions(keptId, s_defaultVersion);
        await Given_active(keptId, s_defaultVersion);
        await Given_active(lostId, new(0, 2, 0));

        await When_starting_up();

        var active = (await ActiveClusters()).Should().ContainSingle().Subject;
        active.Id.Should().Be(keptId);
        (await StoredClusterIds()).Should().Equal(keptId);
    }

    private Task Given_stored_versions(Guid clusterId, params Version[] versions)
        => Given_stored_versions(clusterId, "Default Cluster", versions);

    private async Task Given_stored_versions(Guid clusterId, string name, params Version[] versions)
    {
        foreach (var version in versions)
        {
            await _store.Store(new Cluster { Id = clusterId, Version = version, Name = name }, TestContext.Current.CancellationToken);
        }
    }

    private void Given_info_file_is_missing(Guid clusterId, Version version)
        => _fileSystem.File.Delete(_fileSystem.Path.Combine(_fileSystem.GetSnapshotsPath(_workspaceProvider),
            ClusterSnapshotStore.GetStorageInfoName(clusterId, version)));

    private async Task Given_active(Guid clusterId, Version version)
    {
        TestDbContext.ActiveClusters.Add(new ActiveClusterInfo
        {
            Id = clusterId,
            Version = version,
            Deployed = _deployedBefore,
            DeployedBy = "user",
        });
        await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        TestDbContext.ChangeTracker.Clear();
    }

    private async Task When_starting_up()
    {
        using var scope = _services.CreateScope();
        await new ClusterManagementModuleInitializer().OnPostMigrate(scope.ServiceProvider, TestContext.Current.CancellationToken);
        TestDbContext.ChangeTracker.Clear();
    }

    private Task<List<ActiveClusterInfo>> ActiveClusters()
        => TestDbContext.ActiveClusters.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);

    private async Task<Guid[]> StoredClusterIds()
        => [.. (await _store.GetClusterInfos(null, TestContext.Current.CancellationToken))
            .Select(i => i.Id)
            .Distinct()];

    private async Task<Version[]> StoredVersions(Guid clusterId)
        => [.. (await _store.GetClusterInfos(clusterId, TestContext.Current.CancellationToken))
            .Select(i => i.Version)
            .Order()];
}
