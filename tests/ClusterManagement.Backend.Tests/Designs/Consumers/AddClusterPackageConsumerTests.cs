using AwesomeAssertions;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Designs.Consumers;
using ClusterManagement.Internal.Commands;
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

public class AddClusterPackageConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IPackagesStore _packageStore = Substitute.For<IPackagesStore>();

    public AddClusterPackageConsumerTests()
    {
        _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<AddClusterPackageConsumer>();
            cfg.AddSingleton(_packageStore);
        };
    }

    [Fact]
    public async Task Should_consume_command()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var zipData = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP file signature
        var command = new AddClusterPackage(zipData);
        var dependency = new ClusterDependency { Name = "TestPackage", Version = new Version(1, 0, 0) };

        _packageStore.AddPackageManually(Arg.Is<byte[]>(data => data.SequenceEqual(zipData)), Arg.Any<CancellationToken>())
            .Returns(dependency);

        // Act
        await tester.TestCommand<AddClusterPackage, AddClusterPackageConsumer>(command);

        // Assert
        Assert.True(await tester.Harness.Consumed.Any<AddClusterPackage>(k
            => k.Context.Message.CorrelationId == command.CorrelationId));
    }

    [Fact]
    public async Task Should_publish_success_event_with_package_info()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var zipData = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // ZIP file signature
        var command = new AddClusterPackage(zipData);
        var dependency = new ClusterDependency { Name = "MyPackage", Version = new Version(2, 1, 3) };

        _packageStore.AddPackageManually(Arg.Is<byte[]>(data => data.SequenceEqual(zipData)), Arg.Any<CancellationToken>())
            .Returns(dependency);

        // Act
        await tester.TestCommand<AddClusterPackage, AddClusterPackageConsumer>(command);

        // Assert: Package added event published with correct details
        Assert.True(await tester.Harness.Published.Any<ClusterPackageChanged>(k
            => k.Context.Message.PackageName == dependency.Name
            && k.Context.Message.Version == dependency.Version.ToString()));
    }

    [Fact]
    public async Task Should_call_package_store_with_correct_data()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var zipData = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x01, 0x02, 0x03 };
        var command = new AddClusterPackage(zipData);
        var dependency = new ClusterDependency { Name = "PackageA", Version = new Version(1, 0, 0) };

        _packageStore.AddPackageManually(Arg.Is<byte[]>(data => data.SequenceEqual(zipData)), Arg.Any<CancellationToken>())
            .Returns(dependency);

        // Act
        await tester.TestCommand<AddClusterPackage, AddClusterPackageConsumer>(command);

        // Assert: Verify the package store was called with the correct data
        await _packageStore.Received(1).AddPackageManually(
            Arg.Is<byte[]>(data => data.SequenceEqual(zipData)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_fault_on_invalid_package_and_publish_error_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var zipData = new byte[] { 0x00, 0x01, 0x02 };
        var command = new AddClusterPackage(zipData);

        _packageStore.AddPackageManually(Arg.Is<byte[]>(data => data.SequenceEqual(zipData)), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Invalid package format"));

        // Act
        await tester.TestCommand<AddClusterPackage, AddClusterPackageConsumer>(command);

        // Assert
        Assert.True(await tester.Harness.Published.Any<ClusterPackageChangeFailed>(k
            => k.Context.Message.CorrelationId == command.CorrelationId
            && k.Context.Message.Error.ErrorCode == 200));
    }
}
