using ClusterManagement.Backend.Consumers;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Events;
using ClusterManagement.Tests.Tools;
using ClusterManagement.Tests.Tools.Factories;
using AwesomeAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;
using Assert = Xunit.Assert;

namespace ClusterManagement.Backend.Tests.Consumers;

public class RemoveClusterConsumerTests : TestWithDbContextSqlite<ClusterDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IClusterSnapshotStore _snapshotStore = Substitute.For<IClusterSnapshotStore>();

    public RemoveClusterConsumerTests()
    {
        _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<RemoveClusterConsumer>();
            cfg.AddSingleton<IClusterDbContext>(_ => TestDbContext);
            cfg.AddSingleton(_snapshotStore);
        };
    }

    [Fact]
    public async Task Command_should_be_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new RemoveCluster(new Guid(), new Version(1, 0, 0));

        // Act
        await tester.TestCommand<RemoveCluster, RemoveClusterConsumer>(command);

        // Assert
        var consumerHarness = tester.Services.GetRequiredService<IConsumerTestHarness<RemoveClusterConsumer>>();

        // did the actual consumer consume the message
        Assert.True(await consumerHarness.Consumed.Any<RemoveCluster>());
    }

    [Fact]
    public async Task Delete_unknown_cluster_should_not_publish_change_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new RemoveCluster(Guid.NewGuid(), new Version(1, 0, 0));

        // Act
        await tester.TestCommand<RemoveCluster, RemoveClusterConsumer>(command);

        // Assert
        Assert.False(await tester.Harness.Published.Any<ClusterChanged>());
    }

    [Fact]
    public async Task Delete_cluster_version_should_publish_change_event()
    {
        //Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var clusterId = Guid.NewGuid();
        var clusterVersion = new Version(1, 0, 0);
        _snapshotStore.Exists(Arg.Is(clusterId), Arg.Is(clusterVersion))
            .Returns(true);

        var command = new RemoveCluster(clusterId, clusterVersion);

        //Act
        await tester.TestCommand<RemoveCluster, RemoveClusterConsumer>(command);

        //Assert: Cluster as expected
        Assert.True(await tester.Harness.Published.Any<ClusterChanged>());
    }

    [Fact]
    public async Task Delete_active_cluster_version_should_throw_and_fault()
    {
        //Arrange
        await using var tester = new MassTransitTester(_configureServices);
        using var builder = ClusterFactory.CreateComplexCluster();
        var dbContext = tester.Services.GetRequiredService<IClusterDbContext>();
        dbContext.SaveActiveClusterVersion(builder.Cluster);

        _snapshotStore.Exists(Arg.Is(builder.Cluster.Id), Arg.Is(builder.Cluster.Version))
            .Returns(true);

        var command = new RemoveCluster(builder.Cluster.Id, builder.Cluster.Version);
        Task Action() => tester.TestCommandFault<RemoveCluster, RemoveClusterConsumer>(command);

        //Act
        await Assert.ThrowsAsync<InvalidOperationException>(Action);
    }

    [Fact]
    public async Task Throws_if_cluster_version_is_default()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        using var builder = ClusterFactory.CreateComplexCluster();
        var dbContext = tester.Services.GetRequiredService<IClusterDbContext>();
        builder.Editors.Cluster.SetVersion(new(1, 0, 0));
        dbContext.SaveActiveClusterVersion(builder.Cluster);
        _snapshotStore.Exists(Arg.Is(builder.Cluster.Id), Arg.Is(new Version(0, 1, 0)))
            .Returns(true);
        _snapshotStore.Exists(Arg.Is(builder.Cluster.Id), Arg.Is(builder.Cluster.Version))
            .Returns(true);

        // Act
        var command = new RemoveCluster(builder.Cluster.Id, new Version(0, 1, 0));
        var act = () => tester.TestCommandFault<RemoveCluster, RemoveClusterConsumer>(command);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Default cluster can't be deleted");
    }
}
