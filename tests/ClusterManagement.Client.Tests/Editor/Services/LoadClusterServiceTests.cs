using Bunit;
using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Requests;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Testing.Client;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Model;
using ViciOne.Ui.ClusterEditor.Services;
using Xunit;
using ClusterManagement.Client.Services;
using ClusterManagement.Tests.Tools.Factories;
using ClusterManagement.Tests.Tools.Extensions;

namespace ClusterManagement.Client.Tests.Editor.Services;

public class LoadClusterServiceTests
{
    private readonly IDataManagementService _dataManagement = Substitute.For<IDataManagementService>();
    private readonly IClusterService _clusterService = Substitute.For<IClusterService>();
    private readonly IDesignProvider _designProvider = Substitute.For<IDesignProvider>();

    private LoadClusterService SetupService(TestContext ctx, List<Cluster> clusters)
    {
        _clusterService.QueryClusterInfos()
            .Returns([.. clusters.Select(c => new ClusterInfo(c.Id, c.Version, c.Name))]);

        _clusterService.QueryVersions()
            .Returns(clusters.GroupBy(c => c.Id, c => c.Version)
                .ToDictionary(c => c.Key, c => c.ToList()));

        foreach (var cluster in clusters)
        {
            _clusterService.Query(cluster.Id, cluster.Version)
                .Returns(cluster);
        }

        ctx.SetupSuiteServices();

        ctx.Services.AddSingleton(_dataManagement);
        ctx.Services.AddSingleton(_clusterService);
        ctx.Services.AddSingleton(_designProvider);
        ctx.Services.AddScoped<LoadClusterService>();
        return ctx.Services.GetRequiredService<LoadClusterService>();
    }

    [Fact]
    public async Task Try_load_should_fill_cluster_editor()
    {
        // Arrange
        using var builder = ClusterFactory.CreateComplexCluster();
        builder.Cluster.Version = new Version(1, 0, 1);
        var fbDesignIds =
            builder.Cache.FunctionBlockDesigns.Select(k => k.Key)
                .ToArray().AsReadOnly();

        var seeds = new List<Cluster>
        {
            builder.Cluster
        };

        _designProvider.CreateResolver().Returns(builder.DependencyResolver);
        _designProvider.GetFunctionBlockDesignIds(Arg.Any<ClusterDependency[]>()).Returns(fbDesignIds);
        _clusterService.Query(builder.Cluster.Id, builder.Cluster.Version)
            .Returns(builder.Cluster);

        using var ctx = new TestContext();
        var service = SetupService(ctx, seeds);
        var loadMonitor = service.Monitor();

        // Act
        await service.TryLoad(seeds[0].Id, seeds[0].Version);

        // Assert
        loadMonitor.Should().Raise(nameof(LoadClusterService.ClusterLoaded));
        _dataManagement.Received(1).LoadDataflow(
            Arg.Is<ClusterBuilder>(b => b.Cluster.Id == builder.Cluster.Id && b.Cluster.Version == builder.Cluster.Version));
    }

    [Fact]
    public async Task Try_load_latest_version_should_pass_new_builder_to_management()
    {
        // Arrange
        using var builder = ClusterFactory.CreateComplexCluster();
        var builderCopy = builder.Clone();
        var latestCluster = builderCopy.Cluster;
        var fbDesignIds =
            builder.Cache.FunctionBlockDesigns.Select(k => k.Key)
                .ToArray().AsReadOnly();

        var seeds = new List<Cluster>
        {
            builder.Cluster, latestCluster,
        };

        _designProvider.CreateResolver().Returns(builder.DependencyResolver);
        _designProvider.GetFunctionBlockDesignIds(Arg.Any<ClusterDependency[]>()).Returns(fbDesignIds);

        using var ctx = new TestContext();
        var service = SetupService(ctx, seeds);
        var loadMonitor = service.Monitor();

        // Act
        await service.TryLoadLatestVersion();

        // Assert
        loadMonitor.Should().Raise(nameof(LoadClusterService.ClusterLoaded));
        _dataManagement.Received(1).LoadDataflow(
            Arg.Is<ClusterBuilder>(b => b.Cluster.Id == latestCluster.Id && b.Cluster.Version == latestCluster.Version));
    }
}
