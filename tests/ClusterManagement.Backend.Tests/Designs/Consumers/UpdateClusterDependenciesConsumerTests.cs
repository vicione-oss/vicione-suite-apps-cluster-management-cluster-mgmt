using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Designs.Consumers;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute.ExceptionExtensions;
using Sdk.Messaging;
using Sdk.Testing.Backend;
using ViciOne.Cluster.Model;
using Assert = Xunit.Assert;

namespace ClusterManagement.Backend.Tests.Designs.Consumers;

public class UpdateClusterDependenciesConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IClusterDependencyStore _dependencyStore = Substitute.For<IClusterDependencyStore>();

    public UpdateClusterDependenciesConsumerTests()
    {
        _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<UpdateClusterDependenciesConsumer>();
            cfg.AddSingleton(_dependencyStore);
        };
    }

    [Fact]
    public async Task Should_consume_command()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var operations = new List<ClusterDependencyOperation>
        {
            new(new ClusterDependency { Name = "TestPackage", Version = "1.0.0" }, ClusterDependencyOperationKind.Install)
        };
        var command = new UpdateClusterDependencies(operations);

        _dependencyStore.EnqueueOperations(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await tester.TestCommand<UpdateClusterDependencies, UpdateClusterDependenciesConsumer>(command);

        // Assert
        Assert.True(await tester.Harness.Consumed.Any<UpdateClusterDependencies>(k
            => k.Context.Message.CorrelationId == command.CorrelationId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_publish_success_event_with_operations()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);

        var installOperation = new ClusterDependencyOperation(new ClusterDependency
        {
            Name = "Package1",
            Version = "1.0.0"
        }, ClusterDependencyOperationKind.Install);

        var uninstallOperation = new ClusterDependencyOperation(new ClusterDependency
        {
            Name = "Package2",
            Version = "2.1.0"
        }, ClusterDependencyOperationKind.Uninstall);

        var changes = new List<ClusterDependencyChange>
        {
            new(CrudAction.Created, installOperation),
            new(CrudAction.Deleted, uninstallOperation)
        };
        var command = new UpdateClusterDependencies([installOperation, uninstallOperation]);

        _dependencyStore.EnqueueOperations(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Returns(changes);

        // Act
        await tester.TestCommand<UpdateClusterDependencies, UpdateClusterDependenciesConsumer>(command);

        // Assert: Dependencies updated event published with correct operations
        Assert.True(await tester.Harness.Published.Any<ClusterDependenciesChanged>(k
            => k.Context.Message.Changes.Count == changes.Count, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_call_dependency_store_with_correct_operations()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var operations = new List<ClusterDependencyOperation>
        {
            new(new ClusterDependency { Name = "PackageA", Version = "1.2.3" }, ClusterDependencyOperationKind.Install)
        };
        var command = new UpdateClusterDependencies(operations);

        _dependencyStore.EnqueueOperations(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await tester.TestCommand<UpdateClusterDependencies, UpdateClusterDependenciesConsumer>(command);

        // Assert: Verify the dependency store was called with the correct operations
        await _dependencyStore.Received(1).EnqueueOperations(
            Arg.Is<List<ClusterDependencyOperation>>(ops => ops.SequenceEqual(operations)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_fault_on_queue_error_and_publish_error_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var operations = new List<ClusterDependencyOperation>
        {
            new(new ClusterDependency { Name = "InvalidPackage", Version = "1.0.0" }, ClusterDependencyOperationKind.Install)
        };
        var command = new UpdateClusterDependencies(operations);

        _dependencyStore.EnqueueOperations(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Failed to queue operations"));

        // Act
        await tester.TestCommand<UpdateClusterDependencies, UpdateClusterDependenciesConsumer>(command);

        // Assert        
        Assert.True(await tester.Harness.Published.Any<ClusterDependenciesChanged>(k
            => k.Context.Message.CorrelationId == command.CorrelationId
            && k.Context.Message.Error is not null
            && k.Context.Message.Error.ErrorCode == 230, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_handle_empty_operations_list()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var operations = new List<ClusterDependencyOperation>();
        var command = new UpdateClusterDependencies(operations);

        _dependencyStore.EnqueueOperations(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        await tester.TestCommand<UpdateClusterDependencies, UpdateClusterDependenciesConsumer>(command);

        // Assert: Empty operations should still succeed
        Assert.True(await tester.Harness.Published.Any<ClusterDependenciesChanged>(k
            => k.Context.Message.Changes.Count == 0, TestContext.Current.CancellationToken));

        await _dependencyStore.Received(1).EnqueueOperations(
            Arg.Is<List<ClusterDependencyOperation>>(ops => ops.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_handle_multiple_install_operations()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);

        var installOperation1 = new ClusterDependencyOperation(new ClusterDependency
        {
            Name = "Package1",
            Version = "1.0.0"
        }, ClusterDependencyOperationKind.Install);

        var installOperation2 = new ClusterDependencyOperation(new ClusterDependency
        {
            Name = "Package2",
            Version = "2.1.0"
        }, ClusterDependencyOperationKind.Install);

        var installOperation3 = new ClusterDependencyOperation(new ClusterDependency
        {
            Name = "Package3",
            Version = "3.4.80"
        }, ClusterDependencyOperationKind.Install);

        var changes = new List<ClusterDependencyChange>
        {
            new(CrudAction.Created, installOperation1),
            new(CrudAction.Created, installOperation2),
            new(CrudAction.Created, installOperation3)
        };

        var command = new UpdateClusterDependencies([installOperation1, installOperation2, installOperation3]);

        _dependencyStore.EnqueueOperations(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Returns(changes);

        // Act
        await tester.TestCommand<UpdateClusterDependencies, UpdateClusterDependenciesConsumer>(command);

        // Assert: All operations should be queued
        await _dependencyStore.Received(1).EnqueueOperations(
            Arg.Is<List<ClusterDependencyOperation>>(ops => ops.Count == changes.Count),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_handle_mixed_install_and_uninstall_operations()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var installOperationA = new ClusterDependencyOperation(new ClusterDependency
        {
            Name = "PackageA",
            Version = "1.0.0"
        }, ClusterDependencyOperationKind.Install);

        var uninstallOperationB = new ClusterDependencyOperation(new ClusterDependency
        {
            Name = "PackageB",
            Version = "2.1.0"
        }, ClusterDependencyOperationKind.Uninstall);

        var installOperationC = new ClusterDependencyOperation(new ClusterDependency
        {
            Name = "PackageC",
            Version = "3.4.80"
        }, ClusterDependencyOperationKind.Install);

        var changes = new List<ClusterDependencyChange>
        {
            new(CrudAction.Created, installOperationA),
            new(CrudAction.Created, uninstallOperationB),
            new(CrudAction.Created, installOperationC)
        };
        var command = new UpdateClusterDependencies([installOperationA, uninstallOperationB, installOperationC]);

        _dependencyStore.EnqueueOperations(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Returns(changes);

        // Act
        await tester.TestCommand<UpdateClusterDependencies, UpdateClusterDependenciesConsumer>(command);

        // Assert: Mixed operations should be handled correctly
        Assert.True(await tester.Harness.Published.Any<ClusterDependenciesChanged>(k
            => k.Context.Message.Changes.Count == 3
            && k.Context.Message.Changes[0].Operation.OperationKind == ClusterDependencyOperationKind.Install
            && k.Context.Message.Changes[1].Operation.OperationKind == ClusterDependencyOperationKind.Uninstall
            && k.Context.Message.Changes[2].Operation.OperationKind == ClusterDependencyOperationKind.Install, TestContext.Current.CancellationToken));
    }
}
