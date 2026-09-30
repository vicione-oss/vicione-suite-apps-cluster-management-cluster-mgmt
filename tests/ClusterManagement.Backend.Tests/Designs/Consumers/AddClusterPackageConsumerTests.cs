using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Designs.Consumers;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using ClusterManagement.Internal.Contracts;
using Sdk.Backend.Modules;
using Sdk.Testing.Backend;
using ViciOne.Cluster.Model;
using Assert = Xunit.Assert;

namespace ClusterManagement.Backend.Tests.Designs.Consumers;

public class AddClusterPackageConsumerTests
{
    private static readonly string CacheDirectory = MockUnixSupport.Path(@"c:\cache");
    private static readonly string UploadDirectory = Path.Combine(CacheDirectory, ClusterPackageUpload.DirectoryName);
    private static readonly string ValidZipFilename = Path.Combine(UploadDirectory, "valid-package.zip");
    private static readonly string InvalidZipFilename = Path.Combine(UploadDirectory, "invalid-package.zip");
    private static readonly string OutsideFilename = Path.Combine(CacheDirectory, "appsettings.json");

    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IPackageStore _packageStore = Substitute.For<IPackageStore>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();

    public AddClusterPackageConsumerTests()
    {
        _workspaceProvider.Cache.Returns(CacheDirectory);

        _fileSystem.AddFile(ValidZipFilename, new MockFileData([0x50, 0x4B, 0x03, 0x04]));
        _fileSystem.AddFile(InvalidZipFilename, "");
        _fileSystem.AddFile(OutsideFilename, "{}");

        _configureServices = cfg =>
        {
            // consumer needs
            cfg.AddConsumer<AddClusterPackageConsumer>();
            cfg.AddSingleton(_packageStore);
            cfg.AddSingleton<IFileSystem>(_fileSystem);
            cfg.AddSingleton(_workspaceProvider);
            cfg.AddSingleton(Substitute.For<ILogger<AddClusterPackageConsumer>>());
        };
    }

    public static TheoryData<string> FilesOutsideUploadDirectory => new()
    {
        OutsideFilename,
        Path.Combine(UploadDirectory, "..", "appsettings.json"),
    };

    [Fact]
    public async Task Should_consume_command()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddClusterPackage(ValidZipFilename);
        var dependency = new ClusterDependency { Name = "TestPackage", Version = "1.0.0" };

        _packageStore.StorePackage(Arg.Is<string>(s => s == ValidZipFilename), true, Arg.Any<CancellationToken>())
            .Returns(dependency);

        // Act
        await tester.TestCommand<AddClusterPackage, AddClusterPackageConsumer>(command);

        // Assert
        Assert.True(await tester.Harness.Consumed.Any<AddClusterPackage>(k
            => k.Context.Message.CorrelationId == command.CorrelationId, TestContext.Current.CancellationToken));

        _fileSystem.File.Exists(ValidZipFilename).Should().BeFalse();
    }

    [Fact]
    public async Task Should_publish_success_event_with_package_info()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddClusterPackage(ValidZipFilename);
        var dependency = new ClusterDependency { Name = "MyPackage", Version = "2.1.3" };

        _packageStore.StorePackage(Arg.Is<string>(s => s == ValidZipFilename), true, Arg.Any<CancellationToken>())
            .Returns(dependency);

        // Act
        await tester.TestCommand<AddClusterPackage, AddClusterPackageConsumer>(command);

        // Assert: Package added event published with correct details
        Assert.True(await tester.Harness.Published.Any<ClusterPackageChanged>(k
            => k.Context.Message.PackageName == dependency.Name
            && k.Context.Message.Version == dependency.Version.ToString(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_call_package_store_with_correct_data()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddClusterPackage(ValidZipFilename);
        var dependency = new ClusterDependency { Name = "PackageA", Version = "1.0.0" };

        _packageStore.StorePackage(Arg.Is<string>(s => s == ValidZipFilename), true, Arg.Any<CancellationToken>())
            .Returns(dependency);

        // Act
        await tester.TestCommand<AddClusterPackage, AddClusterPackageConsumer>(command);

        // Assert: Verify the package store was called with the correct data
        await _packageStore.Received(1).StorePackage(
            Arg.Is<string>(s => s == ValidZipFilename),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_publish_validation_error_without_fault_on_invalid_package()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddClusterPackage(InvalidZipFilename);

        _packageStore.StorePackage(Arg.Is<string>(s => s == InvalidZipFilename), true, Arg.Any<CancellationToken>())
            .Throws(new PackageValidationException(ClusterPackageChanged.InvalidPackageContent, "Invalid package format"));

        // Act
        await tester.TestCommand<AddClusterPackage, AddClusterPackageConsumer>(command);

        // Assert
        Assert.True(await tester.Harness.Published.Any<ClusterPackageChanged>(k
            => k.Context.Message.CorrelationId == command.CorrelationId
            && k.Context.Message.Error is not null
            && k.Context.Message.Error.ErrorCode == ClusterPackageChanged.InvalidPackageContent, TestContext.Current.CancellationToken));

        Assert.False(await tester.Harness.Published.Any<Fault<AddClusterPackage>>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_delete_package_file_when_package_is_invalid()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddClusterPackage(InvalidZipFilename);

        _packageStore.StorePackage(Arg.Is<string>(s => s == InvalidZipFilename), true, Arg.Any<CancellationToken>())
            .Throws(new PackageValidationException(ClusterPackageChanged.NotAZipArchive, "Not a zip archive"));

        // Act
        await tester.TestCommand<AddClusterPackage, AddClusterPackageConsumer>(command);

        // Assert
        Assert.True(await tester.Harness.Published.Any<ClusterPackageChanged>(k
            => k.Context.Message.CorrelationId == command.CorrelationId, TestContext.Current.CancellationToken));

        _fileSystem.File.Exists(InvalidZipFilename).Should().BeFalse();
    }

    [Fact]
    public async Task Should_publish_file_not_found_error_when_package_file_is_missing()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddClusterPackage(Path.Combine(UploadDirectory, "missing-package.zip"));

        // Act
        await tester.TestCommand<AddClusterPackage, AddClusterPackageConsumer>(command);

        // Assert
        Assert.True(await tester.Harness.Published.Any<ClusterPackageChanged>(k
            => k.Context.Message.CorrelationId == command.CorrelationId
            && k.Context.Message.Error is not null
            && k.Context.Message.Error.ErrorCode == ClusterPackageChanged.PackageFileNotFound, TestContext.Current.CancellationToken));

        await _packageStore.DidNotReceive().StorePackage(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_fault_on_unexpected_error_and_publish_error_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddClusterPackage(ValidZipFilename);

        _packageStore.StorePackage(Arg.Is<string>(s => s == ValidZipFilename), true, Arg.Any<CancellationToken>())
            .Throws(new IOException("Disk unavailable"));

        // Act
        var act = () => tester.TestCommandFault<AddClusterPackage, AddClusterPackageConsumer>(command);

        // Assert
        await act.Should().ThrowAsync<IOException>().WithMessage("Disk unavailable");

        Assert.True(await tester.Harness.Published.Any<ClusterPackageChanged>(k
            => k.Context.Message.CorrelationId == command.CorrelationId
            && k.Context.Message.Error is not null
            && k.Context.Message.Error.ErrorCode == ClusterPackageChanged.AddPackageFailed, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_delete_package_file_once_the_command_faults()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddClusterPackage(ValidZipFilename);

        _packageStore.StorePackage(Arg.Is<string>(s => s == ValidZipFilename), true, Arg.Any<CancellationToken>())
            .Throws(new IOException("Disk unavailable"));

        // Act
        var act = () => tester.TestCommandFault<AddClusterPackage, AddClusterPackageConsumer>(command);

        // Assert: the fault consumer deletes the file before it publishes the error event
        await act.Should().ThrowAsync<IOException>();

        Assert.True(await tester.Harness.Published.Any<ClusterPackageChanged>(k
            => k.Context.Message.CorrelationId == command.CorrelationId, TestContext.Current.CancellationToken));

        _fileSystem.File.Exists(ValidZipFilename).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(FilesOutsideUploadDirectory))]
    public async Task Should_reject_package_file_outside_upload_directory_without_touching_it(string filename)
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddClusterPackage(filename);

        // Act
        await tester.TestCommand<AddClusterPackage, AddClusterPackageConsumer>(command);

        // Assert
        Assert.True(await tester.Harness.Published.Any<ClusterPackageChanged>(k
            => k.Context.Message.CorrelationId == command.CorrelationId
            && k.Context.Message.Error is not null
            && k.Context.Message.Error.ErrorCode == ClusterPackageChanged.PackageFileOutsideUploadDirectory, TestContext.Current.CancellationToken));

        await _packageStore.DidNotReceive().StorePackage(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        _fileSystem.File.Exists(OutsideFilename).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(FilesOutsideUploadDirectory))]
    public async Task Should_not_delete_file_outside_upload_directory_on_fault(string filename)
    {
        // Arrange: a fault can be published by anyone on the bus, so it does not pass the check of the command consumer
        var consumer = new AddClusterPackageConsumer(_packageStore, _fileSystem, _workspaceProvider, Substitute.For<ILogger<AddClusterPackageConsumer>>());

        var fault = Substitute.For<Fault<AddClusterPackage>>();
        fault.Message.Returns(new AddClusterPackage(filename));
        fault.Exceptions.Returns([]);

        var context = Substitute.For<ConsumeContext<Fault<AddClusterPackage>>>();
        context.Message.Returns(fault);

        // Act
        await consumer.Consume(context);

        // Assert
        _fileSystem.File.Exists(OutsideFilename).Should().BeTrue();
    }
}
