using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Designs.Consumers;
using ClusterManagement.Internal.Events;
using ClusterManagement.Public.Iodds.Commands;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sdk.Testing.Backend;
using ViciOne.Cluster.Model;
using Xunit;
using Assert = Xunit.Assert;

namespace ClusterManagement.Backend.Tests.Designs.Consumers;

public class DeleteClusterPackageConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IPackageStore _packageStore = Substitute.For<IPackageStore>();

    public DeleteClusterPackageConsumerTests()
    {
        _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<DeleteClusterPackageConsumer>();
            cfg.AddSingleton(_packageStore);
        };
    }

    [Fact]
    public async Task Should_consume_command()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteClusterPackage("TestPackage", "1.0.0");

        _packageStore.DeletePackage(Arg.Any<ClusterDependency>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await tester.TestCommand<DeleteClusterPackage, DeleteClusterPackageConsumer>(command);

        // Assert
        Assert.True(await tester.Harness.Consumed.Any<DeleteClusterPackage>(k
            => k.Context.Message.CorrelationId == command.CorrelationId));
    }

    [Fact]
    public async Task Should_publish_success_event_with_package_info()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteClusterPackage("MyPackage", "2.1.3");

        _packageStore.DeletePackage(Arg.Any<ClusterDependency>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await tester.TestCommand<DeleteClusterPackage, DeleteClusterPackageConsumer>(command);

        // Assert: Package deleted event published with correct details
        Assert.True(await tester.Harness.Published.Any<ClusterPackageChanged>(k
            => k.Context.Message.PackageName == command.PackageName
            && k.Context.Message.Version == command.Version
            && k.Context.Message.Change == ClusterPackageChange.Delete));
    }

    [Fact]
    public async Task Should_call_package_store_with_correct_dependency()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteClusterPackage("PackageA", "1.0.0");

        _packageStore.DeletePackage(Arg.Any<ClusterDependency>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await tester.TestCommand<DeleteClusterPackage, DeleteClusterPackageConsumer>(command);

        // Assert: Verify the package store was called with the correct dependency
        await _packageStore.Received(1).DeletePackage(
            Arg.Is<ClusterDependency>(dep => dep.Name == command.PackageName && dep.Version.ToString() == command.Version),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_fault_on_delete_error_and_publish_error_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteClusterPackage("NonExistentPackage", "1.0.0");

        _packageStore.DeletePackage(Arg.Any<ClusterDependency>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Package not found"));

        // Act
        await tester.TestCommand<DeleteClusterPackage, DeleteClusterPackageConsumer>(command);

        // Assert        
        Assert.True(await tester.Harness.Published.Any<ClusterPackageChangeFailed>(k
            => k.Context.Message.CorrelationId == command.CorrelationId
            && k.Context.Message.Error.ErrorCode == 200
            && k.Context.Message.Change == ClusterPackageChange.Delete));
    }

    [Fact]
    public async Task Should_preserve_correlation_id_in_success_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteClusterPackage("TestPackage", "1.0.0");

        _packageStore.DeletePackage(Arg.Any<ClusterDependency>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await tester.TestCommand<DeleteClusterPackage, DeleteClusterPackageConsumer>(command);

        // Assert: Verify correlation ID is preserved
        Assert.True(await tester.Harness.Published.Any<ClusterPackageChanged>(k
            => k.Context.Message.CorrelationId == command.CorrelationId));
    }
}
