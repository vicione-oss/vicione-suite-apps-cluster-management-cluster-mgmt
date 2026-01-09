using System.IO.Compression;
using AwesomeAssertions;
using ClusterManagement.Backend.Iodds.Consumers;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace ClusterManagement.Backend.Tests.Iodds.Consumers;

public class DownloadIoddConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IIoddStore _ioddStore = Substitute.For<IIoddStore>();

    public DownloadIoddConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<DownloadIoddConsumer>();
            cfg.AddSingleton(_ioddStore);
        };

    [Fact]
    public async Task Request_should_return_device_archive_when_device_exists()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var filename = "ifm-myDevice-20250323-IODD1.1.xml";
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(vendorId, deviceId, filename);
        var ioddId = new IoddId(vendorId, deviceId);

        var request = new DownloadIoddRequest { Id = ioddId };

        _ioddStore.Devices.Returns([deviceInfo]);
        _ioddStore.When(x => x.FillDeviceArchive(Arg.Any<ZipArchive>(), Arg.Any<IoddId>()))
            .Do(x =>
            {
                var archive = x.ArgAt<ZipArchive>(0);
                var entry = archive.CreateEntry(filename);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write(IoddConstants.IoddDeviceTwo);
            });

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        response.Content.Should().NotBeEmpty();
        response.Filename.Should().Be(filename);
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_return_error_when_device_does_not_exist()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 999;
        var deviceId = 888;
        var ioddId = new IoddId(vendorId, deviceId);

        var request = new DownloadIoddRequest { Id = ioddId };

        _ioddStore.Devices.Returns([]);

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        response.RequestError.Should().NotBeNull();
        response.RequestError.ErrorCode.Should().Be(400);
        response.RequestError.Message.Should().Be("failed to download iodd");
    }

    [Fact]
    public async Task Request_should_find_device_by_vendor_and_device_id()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var filename = "device.xml";
        var deviceInfo1 = DeviceInfoFactory.CreateDeviceInfo(310, 2, filename);
        var deviceInfo2 = DeviceInfoFactory.CreateDeviceInfo(311, 3, "other.xml");
        var ioddId = new IoddId(vendorId, deviceId);

        var request = new DownloadIoddRequest { Id = ioddId };

        _ioddStore.Devices.Returns([deviceInfo1, deviceInfo2]);
        _ioddStore.When(x => x.FillDeviceArchive(Arg.Any<ZipArchive>(), Arg.Any<IoddId>()))
            .Do(x =>
            {
                var archive = x.ArgAt<ZipArchive>(0);
                var entry = archive.CreateEntry(filename);
            });

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        response.Filename.Should().Be(filename);
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_call_FillDeviceArchive_with_correct_parameters()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(vendorId, deviceId, "device.xml");
        var ioddId = new IoddId(vendorId, deviceId);

        var request = new DownloadIoddRequest { Id = ioddId };

        _ioddStore.Devices.Returns([deviceInfo]);

        // Act
        await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        _ioddStore.Received(1).FillDeviceArchive(Arg.Any<ZipArchive>(), Arg.Is<IoddId>(id =>
            id.VendorId == vendorId && id.DeviceId == deviceId));
    }

    [Fact]
    public async Task Request_should_return_valid_zip_archive()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var filename = "test-device.xml";
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(vendorId, deviceId, filename);
        var ioddId = new IoddId(vendorId, deviceId);

        var request = new DownloadIoddRequest { Id = ioddId };

        _ioddStore.Devices.Returns([deviceInfo]);
        _ioddStore.When(x => x.FillDeviceArchive(Arg.Any<ZipArchive>(), Arg.Any<IoddId>()))
            .Do(x =>
            {
                var archive = x.ArgAt<ZipArchive>(0);
                var entry = archive.CreateEntry("test.xml");
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write("test content");
            });

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        response.Content.Should().NotBeEmpty();

        // Verify it's a valid ZIP archive
        using var stream = new MemoryStream(response.Content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        archive.Entries.Should().ContainSingle();
        archive.Entries[0].Name.Should().Be("test.xml");
    }

    [Fact]
    public async Task Request_should_handle_multiple_files_in_archive()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var filename = "device.xml";
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(vendorId, deviceId, filename);
        var ioddId = new IoddId(vendorId, deviceId);

        var request = new DownloadIoddRequest { Id = ioddId };

        _ioddStore.Devices.Returns([deviceInfo]);
        _ioddStore.When(x => x.FillDeviceArchive(Arg.Any<ZipArchive>(), Arg.Any<IoddId>()))
            .Do(x =>
            {
                var archive = x.ArgAt<ZipArchive>(0);

                var entry1 = archive.CreateEntry("device.xml");
                using (var stream1 = entry1.Open())
                {
                    using var writer1 = new StreamWriter(stream1);
                    writer1.Write("xml content");
                }

                var entry2 = archive.CreateEntry("image.png");
                using (var stream2 = entry2.Open())
                {
                    stream2.Write(new byte[] { 0x01, 0x02, 0x03 });
                }
            });

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        using var stream = new MemoryStream(response.Content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        archive.Entries.Should().HaveCount(2);
        archive.Entries.Should().Contain(e => e.Name == "device.xml");
        archive.Entries.Should().Contain(e => e.Name == "image.png");
    }

    [Fact]
    public async Task Request_should_return_correct_filename()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var expectedFilename = "ifm-device-20250101-IODD1.0.xml";
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(vendorId, deviceId, expectedFilename);
        var ioddId = new IoddId(vendorId, deviceId);

        var request = new DownloadIoddRequest { Id = ioddId };

        _ioddStore.Devices.Returns([deviceInfo]);
        _ioddStore.When(x => x.FillDeviceArchive(Arg.Any<ZipArchive>(), Arg.Any<IoddId>()))
            .Do(x =>
            {
                var archive = x.ArgAt<ZipArchive>(0);
                var entry = archive.CreateEntry("file.xml");
            });

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        response.Filename.Should().Be(expectedFilename);
    }

    [Fact]
    public async Task Request_should_handle_exception_from_FillDeviceArchive()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(vendorId, deviceId, "device.xml");
        var ioddId = new IoddId(vendorId, deviceId);

        var request = new DownloadIoddRequest { Id = ioddId };

        _ioddStore.Devices.Returns([deviceInfo]);
        _ioddStore.When(x => x.FillDeviceArchive(Arg.Any<ZipArchive>(), Arg.Any<IoddId>()))
            .Do(_ => throw new InvalidOperationException("Archive error"));

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        response.RequestError.Should().NotBeNull();
        response.RequestError.ErrorCode.Should().Be(400);
    }

    [Fact]
    public async Task Request_should_throw_ArgumentException_when_device_not_found()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var ioddId = new IoddId(vendorId, deviceId);
        var otherDevice = DeviceInfoFactory.CreateDeviceInfo(311, 3, "other.xml");

        var request = new DownloadIoddRequest { Id = ioddId };

        _ioddStore.Devices.Returns([otherDevice]);

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        response.RequestError.Should().NotBeNull();
        response.RequestError.ErrorCode.Should().Be(400);
    }

    [Fact]
    public async Task Request_should_handle_empty_device_collection()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var ioddId = new IoddId(310, 2);

        var request = new DownloadIoddRequest { Id = ioddId };

        _ioddStore.Devices.Returns([]);

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        response.RequestError.Should().NotBeNull();
        response.RequestError.ErrorCode.Should().Be(400);
    }

    [Fact]
    public async Task Request_should_return_non_empty_content_array()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(vendorId, deviceId, "device.xml");
        var ioddId = new IoddId(vendorId, deviceId);

        var request = new DownloadIoddRequest { Id = ioddId };

        _ioddStore.Devices.Returns([deviceInfo]);
        _ioddStore.When(x => x.FillDeviceArchive(Arg.Any<ZipArchive>(), Arg.Any<IoddId>()))
            .Do(x =>
            {
                var archive = x.ArgAt<ZipArchive>(0);
                var entry = archive.CreateEntry("file.xml");
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write("content");
            });

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        response.Content.Should().NotBeNull();
        response.Content.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Request_should_create_valid_zip_stream()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(vendorId, deviceId, "device.xml");
        var ioddId = new IoddId(vendorId, deviceId);
        var testContent = "test iodd content";

        var request = new DownloadIoddRequest { Id = ioddId };

        _ioddStore.Devices.Returns([deviceInfo]);
        _ioddStore.When(x => x.FillDeviceArchive(Arg.Any<ZipArchive>(), Arg.Any<IoddId>()))
            .Do(x =>
            {
                var archive = x.ArgAt<ZipArchive>(0);
                var entry = archive.CreateEntry("iodd.xml");
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write(testContent);
            });

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        using var stream = new MemoryStream(response.Content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.Entries[0];
        using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream);
        var content = reader.ReadToEnd();

        content.Should().Be(testContent);
    }
}
