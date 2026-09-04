using ClusterManagement.Backend.Consumers;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Events;
using ClusterManagement.Tests.Tools;
using ClusterManagement.Tests.Tools.Factories;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;
using Assert = Xunit.Assert;

namespace ClusterManagement.Backend.Tests.Consumers;

public class RemoveClusterConsumerTests : TestWithDbContextSqlite<ClusterDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IClusterSnapshotStore _snapshotStore = Substitute.For<IClusterSnapshotStore>();

    public RemoveClusterConsumerTests()
        => _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<RemoveClusterConsumer>();
            cfg.AddSingleton<IClusterDbContext>(_ => TestDbContext);
            cfg.AddSingleton(_snapshotStore);
        };

    [Fact]
    public async Task Should_consume_command()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new RemoveCluster(new Guid(), new Version(1, 0, 0));
        _snapshotStore.Exists(Arg.Is(command.ClusterId), Arg.Is(command.Version))
            .Returns(true);

        // Act
        await tester.TestCommand<RemoveCluster, RemoveClusterConsumer>(command);

        // Assert
        var consumerHarness = tester.Services.GetRequiredService<IConsumerTestHarness<RemoveClusterConsumer>>();

        // did the actual consumer consume the message
        Assert.True(await consumerHarness.Consumed.Any<RemoveCluster>(k
            => k.Context.Message.ClusterId == command.ClusterId
            && k.Context.Message.Version == command.Version, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_publish_change_event_on_success()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new RemoveCluster(Guid.NewGuid(), new Version(1, 0, 0));
        _snapshotStore.Exists(Arg.Is(command.ClusterId), Arg.Is(command.Version))
            .Returns(true);

        // Act
        await tester.TestCommand<RemoveCluster, RemoveClusterConsumer>(command);

        // Assert: Cluster as expected
        Assert.True(await tester.Harness.Published.Any<ClusterChanged>(k
            => k.Context.Message.ClusterId == command.ClusterId
            && k.Context.Message.Version == command.Version
            && k.Context.Message.Action == Sdk.Messaging.CrudAction.Deleted, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_fault_if_cluster_version_is_active_and_publish_error_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        using var builder = ClusterFactory.CreateComplexCluster();
        builder.Editors.Cluster.SetVersion(new(1, 0, 0));
        var dbContext = tester.Services.GetRequiredService<IClusterDbContext>();
        dbContext.SaveActiveClusterVersion(builder.Cluster);

        var command = new RemoveCluster(builder.Cluster.Id, builder.Cluster.Version);

        _snapshotStore.Exists(Arg.Is(command.ClusterId), Arg.Is(command.Version))
            .Returns(true);

        // Act
        var act = () => tester.TestCommandFault<RemoveCluster, RemoveClusterConsumer>(command);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Active cluster version '*' can't be deleted");

        Assert.True(await tester.Harness.Published.Any<ClusterError>(k
            => k.Context.Message.ClusterId == command.ClusterId
            && k.Context.Message.Version == command.Version, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_fault_if_cluster_version_is_default_and_publish_error_event()
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

        Assert.True(await tester.Harness.Published.Any<ClusterError>(k
            => k.Context.Message.ClusterId == command.ClusterId
            && k.Context.Message.Version == command.Version, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_fault_if_cluster_version_does_not_exits_and_publish_error_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);

        // Act
        var command = new RemoveCluster(Guid.NewGuid(), new Version(1, 1, 0));
        var act = () => tester.TestCommandFault<RemoveCluster, RemoveClusterConsumer>(command);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Cluster version '*' does not exist");

        Assert.True(await tester.Harness.Published.Any<ClusterError>(k
            => k.Context.Message.ClusterId == command.ClusterId
            && k.Context.Message.Version == command.Version, TestContext.Current.CancellationToken));
    }
}
