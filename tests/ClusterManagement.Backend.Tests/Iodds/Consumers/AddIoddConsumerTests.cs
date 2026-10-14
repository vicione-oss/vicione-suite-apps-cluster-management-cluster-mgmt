using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using ClusterManagement.Backend.Iodds.Consumers;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Commands;
using ClusterManagement.Public.Iodds.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Backend.Modules;
using Sdk.Messaging;
using Sdk.Testing.Backend;

namespace ClusterManagement.Backend.Tests.Iodds.Consumers;

public class AddIoddConsumerTests
{
    private static readonly string s_cacheDirectory = MockUnixSupport.Path(@"c:\cache");
    private static readonly string s_uploadDirectory = Path.Combine(s_cacheDirectory, IoddUpload.DirectoryName);
    private static readonly string s_emptyZipDataFilename = Path.Combine(s_uploadDirectory, "zipData.zip");
    private static readonly string s_invalidZipDataFilename = Path.Combine(s_uploadDirectory, "invalidZipData.zip");
    private static readonly string s_outsideFilename = Path.Combine(s_cacheDirectory, "appsettings.json");

    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IIoddStore _ioddStore = Substitute.For<IIoddStore>();
    private readonly MockFileSystem _filesystem = new();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();

    public AddIoddConsumerTests()
    {
        _workspaceProvider.Cache.Returns(s_cacheDirectory);

        _configureServices = cfg =>
        {
            cfg.AddConsumer<AddIoddConsumer>();
            cfg.AddSingleton(_ioddStore);
            cfg.AddSingleton<IFileSystem>(_filesystem);
            cfg.AddSingleton(_workspaceProvider);
        };

        var stream = new MemoryStream();
        var archive = new ZipArchive(stream, ZipArchiveMode.Create, true);
        archive.Dispose();
        var zipBytes = stream.ToArray();

        _filesystem.AddFile(s_emptyZipDataFilename, new MockFileData(zipBytes));
        _filesystem.AddFile(s_invalidZipDataFilename, new MockFileData([]));
        _filesystem.AddFile(s_outsideFilename, new MockFileData("{}"));
    }

    [Fact]
    public async Task Command_should_be_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var zipData = CreateValidZipArchive();
        var command = new AddIodd(s_emptyZipDataFilename);
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo();

        _ioddStore.AddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(x =>
            {
                x[1] = new IoddId(310, 2);
                return AddDeviceResult.Added;
            });
        _ioddStore.Devices.Returns([deviceInfo]);

        // Act + Assert
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);
    }

    [Fact]
    public async Task Command_should_add_device_successfully()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var zipData = CreateValidZipArchive();
        var command = new AddIodd(s_emptyZipDataFilename);
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo();
        var expectedId = new IoddId(310, 2);

        _ioddStore.AddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(x =>
            {
                x[1] = expectedId;
                return AddDeviceResult.Added;
            });
        _ioddStore.Devices.Returns([deviceInfo]);

        // Act
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);

        // Assert
        _ioddStore.Received(1).AddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>());
    }

    [Fact]
    public async Task Command_should_publish_correlated_changed_event_on_success()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var zipData = CreateValidZipArchive();
        var command = new AddIodd(s_emptyZipDataFilename);
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo();
        var expectedId = new IoddId(310, 2);

        _ioddStore.AddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(x =>
            {
                x[1] = expectedId;
                return AddDeviceResult.Added;
            });
        _ioddStore.Devices.Returns([deviceInfo]);

        // Act
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<IoddChanged>(k
            => k.Context.Message.Iodd.VendorId == expectedId.VendorId
            && k.Context.Message.Iodd.DeviceId == expectedId.DeviceId
            && k.Context.Message.Action == CrudAction.Created
            && k.Context.Message.CorrelationId == command.CorrelationId
            && k.Context.Message.DeviceInfo == deviceInfo,
            TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_publish_already_exists_error_with_existing_device_when_device_already_exists()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddIodd(s_emptyZipDataFilename);
        var existingDevice = DeviceInfoFactory.CreateDeviceInfo(310, 2);
        var existingId = new IoddId(310, 2);

        _ioddStore.AddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(x =>
            {
                x[1] = existingId;
                return AddDeviceResult.AlreadyExists;
            });
        _ioddStore.Devices.Returns([DeviceInfoFactory.CreateDeviceInfo(311, 3), existingDevice]);

        // Act
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<IoddChanged>(k
            => k.Context.Message.Error?.ErrorCode == IoddChanged.IoddAlreadyExists
            && k.Context.Message.Iodd == existingId
            && k.Context.Message.DeviceInfo == existingDevice
            && k.Context.Message.CorrelationId == command.CorrelationId,
            TestContext.Current.CancellationToken)).Should().BeTrue();
        _filesystem.File.Exists(s_emptyZipDataFilename).Should().BeFalse();
    }

    public static TheoryData<AddDeviceResult> FailedAddDeviceResults => [
        AddDeviceResult.NoIoddFile,
        AddDeviceResult.ExtractionFailed,
    ];

    [Theory]
    [MemberData(nameof(FailedAddDeviceResults))]
    public async Task Should_publish_extract_zip_error_when_AddDevice_fails(AddDeviceResult result)
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddIodd(s_emptyZipDataFilename);

        _ioddStore.AddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(x =>
            {
                x[1] = result == AddDeviceResult.NoIoddFile ? null : new IoddId(310, 2);
                return result;
            });

        // Act
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<IoddChanged>(k
            => k.Context.Message.Error?.ErrorCode == IoddChanged.ExtractZipError
            && k.Context.Message.CorrelationId == command.CorrelationId,
            TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_publish_change_with_error_on_handle_invalid_zip_data()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var invalidZipData = new byte[] { 0x00, 0x01, 0x02 };
        var command = new AddIodd(s_invalidZipDataFilename);

        // Act
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<IoddChanged>(k
            => k.Context.Message.Error is not null
            && k.Context.Message.CorrelationId == command.CorrelationId,
            TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_publish_change_with_error_on_handle_empty_zip_archive()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var emptyZipData = CreateEmptyZipArchive();
        var command = new AddIodd(s_emptyZipDataFilename);

        _ioddStore.AddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(AddDeviceResult.NoIoddFile);

        // Act
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<IoddChanged>(k
            => k.Context.Message.Error is not null, TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Command_should_retrieve_correct_device_from_store()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var zipData = CreateValidZipArchive();
        var command = new AddIodd(s_emptyZipDataFilename);
        var deviceInfo1 = DeviceInfoFactory.CreateDeviceInfo(310, 2);
        var deviceInfo2 = DeviceInfoFactory.CreateDeviceInfo(311, 3);
        var expectedId = new IoddId(310, 2);

        _ioddStore.AddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(x =>
            {
                x[1] = expectedId;
                return AddDeviceResult.Added;
            });
        _ioddStore.Devices.Returns([deviceInfo1, deviceInfo2]);

        // Act
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<IoddChanged>(k => k.Context.Message.DeviceInfo == deviceInfo1, TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Command_should_dispose_zip_archive_properly()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var zipData = CreateValidZipArchive();
        var command = new AddIodd(s_emptyZipDataFilename);
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo();

        _ioddStore.AddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(x =>
            {
                x[1] = new IoddId(310, 2);
                return AddDeviceResult.Added;
            });
        _ioddStore.Devices.Returns([deviceInfo]);

        // Act
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);

        // Assert - Should complete without resource leaks
        _ioddStore.Received(1).AddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>());
    }

    [Fact]
    public async Task Should_publish_file_not_found_error_when_uploaded_file_is_missing()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddIodd(Path.Combine(s_uploadDirectory, "missing.zip"));

        // Act
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<IoddChanged>(k
            => k.Context.Message.Error?.ErrorCode == IoddChanged.IoddFileNotFound
            && k.Context.Message.CorrelationId == command.CorrelationId,
            TestContext.Current.CancellationToken)).Should().BeTrue();
        _ioddStore.DidNotReceive().AddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>());
    }

    public static TheoryData<string> OutsideFilenames => [
        s_outsideFilename,
        Path.Combine(s_uploadDirectory, "..", "appsettings.json"),
        Path.Combine(s_uploadDirectory, "sub", "zipData.zip"),
    ];

    [Theory]
    [MemberData(nameof(OutsideFilenames))]
    public async Task Should_reject_file_outside_upload_directory_without_deleting_it(string filename)
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddIodd(filename);

        // Act
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<IoddChanged>(k
            => k.Context.Message.Error?.ErrorCode == IoddChanged.IoddFileOutsideUploadDirectory
            && k.Context.Message.CorrelationId == command.CorrelationId,
            TestContext.Current.CancellationToken)).Should().BeTrue();
        _filesystem.File.Exists(s_outsideFilename).Should().BeTrue();
        _ioddStore.DidNotReceive().AddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>());
    }

    [Fact]
    public async Task Should_delete_uploaded_file_after_success()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddIodd(s_emptyZipDataFilename);

        _ioddStore.AddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(x =>
            {
                x[1] = new IoddId(310, 2);
                return AddDeviceResult.Added;
            });
        _ioddStore.Devices.Returns([DeviceInfoFactory.CreateDeviceInfo()]);

        // Act
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);

        // Assert
        _filesystem.File.Exists(s_emptyZipDataFilename).Should().BeFalse();
    }

    [Fact]
    public async Task Should_delete_uploaded_file_after_invalid_zip()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new AddIodd(s_invalidZipDataFilename);

        // Act
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<IoddChanged>(k
            => k.Context.Message.Error?.ErrorCode == IoddChanged.ExtractZipError,
            TestContext.Current.CancellationToken)).Should().BeTrue();
        _filesystem.File.Exists(s_invalidZipDataFilename).Should().BeFalse();
    }

    private static byte[] CreateValidZipArchive()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("ifm-myDevice-20250323-IODD1.1.xml");
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream);
            writer.Write(IoddConstants.IoddDeviceTwo);
        }
        return memoryStream.ToArray();
    }

    private static byte[] CreateEmptyZipArchive()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // Empty archive
        }
        return memoryStream.ToArray();
    }
}
