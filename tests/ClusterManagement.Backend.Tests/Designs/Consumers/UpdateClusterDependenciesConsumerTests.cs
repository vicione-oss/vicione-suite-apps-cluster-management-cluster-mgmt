using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Designs.Consumers;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Testing.Backend;
using ViciOne.Cluster.Model;
using Xunit;
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
            new(new ClusterDependency { Name = "TestPackage", Version = new Version(1, 0, 0) }, ClusterDependencyOperationKind.Install)
        };
        var command = new UpdateClusterDependencies(operations);

        _dependencyStore.EnqueueOperations(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await tester.TestCommand<UpdateClusterDependencies, UpdateClusterDependenciesConsumer>(command);

        // Assert
        Assert.True(await tester.Harness.Consumed.Any<UpdateClusterDependencies>(k
            => k.Context.Message.CorrelationId == command.CorrelationId));
    }

    [Fact]
    public async Task Should_publish_success_event_with_operations()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var operations = new List<ClusterDependencyOperation>
        {
            new(new ClusterDependency { Name = "Package1", Version = new Version(1, 0, 0) }, ClusterDependencyOperationKind.Install),
            new(new ClusterDependency { Name = "Package2", Version = new Version(2, 1, 0) }, ClusterDependencyOperationKind.Uninstall)
        };
        var command = new UpdateClusterDependencies(operations);

        _dependencyStore.EnqueueOperations(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await tester.TestCommand<UpdateClusterDependencies, UpdateClusterDependenciesConsumer>(command);

        // Assert: Dependencies updated event published with correct operations
        Assert.True(await tester.Harness.Published.Any<ClusterDependenciesUpdated>(k
            => k.Context.Message.Operations.Count == operations.Count
            && k.Context.Message.Operations.SequenceEqual(operations)));
    }

    [Fact]
    public async Task Should_call_dependency_store_with_correct_operations()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var operations = new List<ClusterDependencyOperation>
        {
            new(new ClusterDependency { Name = "PackageA", Version = new Version(1, 2, 3) }, ClusterDependencyOperationKind.Install)
        };
        var command = new UpdateClusterDependencies(operations);

        _dependencyStore.EnqueueOperations(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

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
            new(new ClusterDependency { Name = "InvalidPackage", Version = new Version(1, 0, 0) }, ClusterDependencyOperationKind.Install)
        };
        var command = new UpdateClusterDependencies(operations);

        _dependencyStore.EnqueueOperations(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Failed to queue operations"));

        // Act
        await tester.TestCommand<UpdateClusterDependencies, UpdateClusterDependenciesConsumer>(command);

        // Assert        
        Assert.True(await tester.Harness.Published.Any<ClusterDependenciesUpdateFailed>(k
            => k.Context.Message.CorrelationId == command.CorrelationId
            && k.Context.Message.Error.ErrorCode == 230));
    }

    [Fact]
    public async Task Should_handle_empty_operations_list()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var operations = new List<ClusterDependencyOperation>();
        var command = new UpdateClusterDependencies(operations);

        _dependencyStore.EnqueueOperations(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await tester.TestCommand<UpdateClusterDependencies, UpdateClusterDependenciesConsumer>(command);

        // Assert: Empty operations should still succeed
        Assert.True(await tester.Harness.Published.Any<ClusterDependenciesUpdated>(k
            => k.Context.Message.Operations.Count == 0));

        await _dependencyStore.Received(1).EnqueueOperations(
            Arg.Is<List<ClusterDependencyOperation>>(ops => ops.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_handle_multiple_install_operations()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var operations = new List<ClusterDependencyOperation>
        {
            new(new ClusterDependency { Name = "Package1", Version = new Version(1, 0, 0) }, ClusterDependencyOperationKind.Install),
            new(new ClusterDependency { Name = "Package2", Version = new Version(2, 0, 0) }, ClusterDependencyOperationKind.Install),
            new(new ClusterDependency { Name = "Package3", Version = new Version(3, 0, 0) }, ClusterDependencyOperationKind.Install)
        };
        var command = new UpdateClusterDependencies(operations);

        _dependencyStore.EnqueueOperations(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await tester.TestCommand<UpdateClusterDependencies, UpdateClusterDependenciesConsumer>(command);

        // Assert: All operations should be queued
        await _dependencyStore.Received(1).EnqueueOperations(
            Arg.Is<List<ClusterDependencyOperation>>(ops => ops.Count == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_handle_mixed_install_and_uninstall_operations()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var operations = new List<ClusterDependencyOperation>
        {
            new(new ClusterDependency { Name = "PackageA", Version = new Version(1, 0, 0) }, ClusterDependencyOperationKind.Install),
            new(new ClusterDependency { Name = "PackageB", Version = new Version(2, 0, 0) }, ClusterDependencyOperationKind.Uninstall),
            new(new ClusterDependency { Name = "PackageC", Version = new Version(3, 0, 0) }, ClusterDependencyOperationKind.Install)
        };
        var command = new UpdateClusterDependencies(operations);

        _dependencyStore.EnqueueOperations(Arg.Any<List<ClusterDependencyOperation>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await tester.TestCommand<UpdateClusterDependencies, UpdateClusterDependenciesConsumer>(command);

        // Assert: Mixed operations should be handled correctly
        Assert.True(await tester.Harness.Published.Any<ClusterDependenciesUpdated>(k
            => k.Context.Message.Operations.Count == 3
            && k.Context.Message.Operations[0].Operation == ClusterDependencyOperationKind.Install
            && k.Context.Message.Operations[1].Operation == ClusterDependencyOperationKind.Uninstall
            && k.Context.Message.Operations[2].Operation == ClusterDependencyOperationKind.Install));
    }
}
