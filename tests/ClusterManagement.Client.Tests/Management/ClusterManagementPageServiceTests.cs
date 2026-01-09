using AwesomeAssertions;
using Bunit;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Client.Services;
using ClusterManagement.Public.Enums;
using ClusterManagement.Public.Events;
using ClusterManagement.Tests.Tools;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.Testing.Client;
using Xunit;

namespace ClusterManagement.Client.Tests.Management;

public class ClusterManagementPageServiceTests
{
    private readonly IClusterService _clusterService = Substitute.For<IClusterService>();


    [Fact(Skip = "To be implemented..")]
    public async Task Query_cluster_versions_should_()
    {
        // Arrange
        using var ctx = new TestContext();
        ctx.SetupSuiteServices();
        ctx.Services.AddSingleton(_clusterService);
        ctx.Services.AddSingleton<ClusterManagementPageService>();
        ctx.SetupSuiteServices();

        var service = ctx.Services.GetRequiredService<ClusterManagementPageService>();

        // Act
        await service.QueryClusterVersions(Guid.NewGuid());

        // Assert
        //service.Builder.Should().NotBeNull();
    }

    [Fact]
    public async Task Consume_event_should_trigger_cluster_update_changed()
    {
        // Arrange
        using var ctx = new TestContext();
        ctx.SetupSuiteServices();
        ctx.Services.AddSingleton(_clusterService);
        ctx.Services.AddSingleton<ClusterManagementPageService>();
        var statusEvent = new ClusterUpdateChanged(Guid.NewGuid(), Guid.NewGuid(), new Version(0, 2, 0), "Ready") { Change = ClusterUpdateChange.Update };
        var service = ctx.Services.GetRequiredService<ClusterManagementPageService>();
        using var monitor = service.Monitor();

        // Act
        var context = new ClientContext<ClusterUpdateChanged>(statusEvent, Guid.NewGuid());
        await service.Consume(context, CancellationToken.None);

        // Assert
        monitor.Should()
            .Raise(nameof(ClusterManagementPageService.OnClusterUpdateChanged))
            .WithArgs<ClusterUpdateChanged>(ev => ev == statusEvent);
    }

    [Fact]
    public async Task Consume_event_should_trigger_cluster_completed_event()
    {
        // Arrange
        using var ctx = new TestContext();
        ctx.SetupSuiteServices();
        ctx.Services.AddSingleton(_clusterService);
        ctx.Services.AddSingleton<ClusterManagementPageService>();
        var statusEvent = new ClusterUpdateCompleted(Guid.NewGuid(), Guid.NewGuid(), new Version(0, 2, 0), TimeSpan.FromSeconds(19));
        var service = ctx.Services.GetRequiredService<ClusterManagementPageService>();
        using var monitor = service.Monitor();

        // Act
        var context = new ClientContext<ClusterUpdateCompleted>(statusEvent, Guid.NewGuid());
        await service.Consume(context, CancellationToken.None);

        // Assert
        monitor.Should()
            .Raise(nameof(ClusterManagementPageService.OnClusterUpdateCompleted))
            .WithArgs<ClusterUpdateCompleted>(ev => ev == statusEvent);
    }

    [Fact]
    public async Task Consume_event_should_trigger_cluster_update_failed()
    {
        // Arrange
        using var ctx = new TestContext();
        ctx.SetupSuiteServices();
        ctx.Services.AddSingleton(_clusterService);
        ctx.Services.AddSingleton<ClusterManagementPageService>();
        var statusEvent = new ClusterUpdateFailed(Guid.NewGuid(), Guid.NewGuid(), new Version(0, 2, 0));
        var service = ctx.Services.GetRequiredService<ClusterManagementPageService>();
        using var monitor = service.Monitor();

        // Act
        var context = new ClientContext<ClusterUpdateFailed>(statusEvent, Guid.NewGuid());
        await service.Consume(context, CancellationToken.None);

        // Assert
        monitor.Should()
            .Raise(nameof(ClusterManagementPageService.OnClusterUpdateFailed))
            .WithArgs<ClusterUpdateFailed>(ev => ev == statusEvent);
    }

    [Fact]
    public async Task Download_cluster_json_should_call_js_interop_download_as()
    {
        // Arrange
        using var ctx = new TestContext();
        ctx.SetupSuiteServices();
        ctx.Services.AddSingleton(_clusterService);
        ctx.Services.AddSingleton<ClusterManagementPageService>();

        var service = ctx.Services.GetRequiredService<ClusterManagementPageService>();
        var interop = ctx.Services.GetRequiredService<IJsInterop>();

        var cluster = ClusterDbContextTools.CreateCluster();
        _clusterService.Query(cluster.Id, cluster.Version).Returns(cluster);

        // Act
        await service.DownloadClusterJson(cluster.Id, cluster.Version);

        // Assert
        await interop.Received().DownloadAs(Arg.Any<string>(), Arg.Any<string>());
    }
}
