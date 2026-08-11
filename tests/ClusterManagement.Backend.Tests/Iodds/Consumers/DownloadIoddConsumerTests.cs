using System.IO.Compression;
using ClusterManagement.Backend.Iodds.Consumers;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;

namespace ClusterManagement.Backend.Tests.Iodds.Consumers;

public class DownloadIoddConsumerTests
{
    private const string ExpectedArchiveName = "iodds-ifm electronic gmbh.zip";

    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IIoddStore _ioddStore = Substitute.For<IIoddStore>();

    public DownloadIoddConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<DownloadIoddConsumer>();
            cfg.AddSingleton(_ioddStore);
        };

    /// <summary>
    /// Stubs <see cref="IIoddStore.FillVendorArchive"/> so that it creates the given entries and
    /// reports all requested devices as archived.
    /// </summary>
    private void FillArchiveWith(params (string EntryName, string Content)[] entries)
        => _ioddStore.FillVendorArchive(Arg.Any<ZipArchive>(), Arg.Any<int>(), Arg.Any<IEnumerable<int>>())
            .Returns(x =>
            {
                var archive = x.ArgAt<ZipArchive>(0);
                foreach (var (entryName, content) in entries)
                {
                    var entry = archive.CreateEntry(entryName);
                    using var entryStream = entry.Open();
                    using var writer = new StreamWriter(entryStream);
                    writer.Write(content);
                }

                return x.ArgAt<IEnumerable<int>>(2).ToArray();
            });

    [Fact]
    public async Task Request_should_return_device_archive_when_device_exists()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var filename = "ifm-myDevice-20250323-IODD1.1.xml";
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(vendorId, deviceId, filename);

        var request = new DownloadIoddRequest { VendorId = vendorId, DeviceIds = [deviceId] };

        _ioddStore.Devices.Returns([deviceInfo]);
        FillArchiveWith(($"{vendorId}/{deviceId}/{filename}", IoddConstants.IoddDeviceTwo));

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        response.Content.Should().NotBeEmpty();
        response.Filename.Should().Be(ExpectedArchiveName);
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_return_error_when_device_does_not_exist()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);

        var request = new DownloadIoddRequest { VendorId = 999, DeviceIds = [888] };

        _ioddStore.Devices.Returns([]);

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        response.RequestError.Should().NotBeNull();
        response.RequestError.ErrorCode.Should().Be(400);
        response.RequestError.Message.Should().Be("failed to download iodd");
    }

    [Fact]
    public async Task Request_should_only_archive_devices_of_the_requested_vendor()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var deviceInfo1 = DeviceInfoFactory.CreateDeviceInfo(310, 2, "device.xml");
        var deviceInfo2 = DeviceInfoFactory.CreateDeviceInfo(311, 3, "other.xml");

        var request = new DownloadIoddRequest { VendorId = vendorId, DeviceIds = [deviceId, 3] };

        _ioddStore.Devices.Returns([deviceInfo1, deviceInfo2]);
        FillArchiveWith(($"{vendorId}/{deviceId}/device.xml", "content"));

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        response.Filename.Should().Be(ExpectedArchiveName);
        response.RequestError.Should().BeNull();
        _ioddStore.Received(1).FillVendorArchive(
            Arg.Any<ZipArchive>(),
            vendorId,
            Arg.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { deviceId })));
    }

    [Fact]
    public async Task Request_should_archive_all_selected_devices_of_a_vendor_in_a_single_call()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(vendorId, 2, "device.xml");
        var otherVendorDevice = DeviceInfoFactory.CreateDeviceInfo(311, 3, "other.xml");

        var request = new DownloadIoddRequest { VendorId = vendorId, DeviceIds = [2, 3] };

        _ioddStore.Devices.Returns([deviceInfo, otherVendorDevice]);
        FillArchiveWith(($"{vendorId}/2/device.xml", "content"));

        // Act
        await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        _ioddStore.Received(1).FillVendorArchive(Arg.Any<ZipArchive>(), vendorId, Arg.Any<IEnumerable<int>>());
    }

    [Fact]
    public async Task Request_should_return_valid_zip_archive()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(vendorId, deviceId, "test-device.xml");

        var request = new DownloadIoddRequest { VendorId = vendorId, DeviceIds = [deviceId] };

        _ioddStore.Devices.Returns([deviceInfo]);
        FillArchiveWith(($"{vendorId}/{deviceId}/test.xml", "test content"));

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        response.Content.Should().NotBeEmpty();

        // Verify it's a valid ZIP archive
        using var stream = new MemoryStream(response.Content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        archive.Entries.Should().ContainSingle();
        archive.Entries[0].FullName.Should().Be($"{vendorId}/{deviceId}/test.xml");
    }

    [Fact]
    public async Task Request_should_handle_multiple_files_in_archive()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(vendorId, deviceId, "device.xml");

        var request = new DownloadIoddRequest { VendorId = vendorId, DeviceIds = [deviceId] };

        _ioddStore.Devices.Returns([deviceInfo]);
        FillArchiveWith(
            ($"{vendorId}/{deviceId}/device.xml", "xml content"),
            ($"{vendorId}/{deviceId}/image.png", "png content"));

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
    public async Task Request_should_return_vendor_archive_filename()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(vendorId, deviceId, "ifm-device-20250101-IODD1.0.xml");

        var request = new DownloadIoddRequest { VendorId = vendorId, DeviceIds = [deviceId] };

        _ioddStore.Devices.Returns([deviceInfo]);
        FillArchiveWith(($"{vendorId}/{deviceId}/file.xml", "content"));

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        response.Filename.Should().Be(ExpectedArchiveName);
    }

    [Fact]
    public async Task Request_should_handle_exception_from_FillVendorArchive()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(vendorId, deviceId, "device.xml");

        var request = new DownloadIoddRequest { VendorId = vendorId, DeviceIds = [deviceId] };

        _ioddStore.Devices.Returns([deviceInfo]);
        _ioddStore.FillVendorArchive(Arg.Any<ZipArchive>(), Arg.Any<int>(), Arg.Any<IEnumerable<int>>())
            .Returns(_ => throw new InvalidOperationException("Archive error"));

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        response.RequestError.Should().NotBeNull();
        response.RequestError.ErrorCode.Should().Be(400);
    }

    [Fact]
    public async Task Request_should_return_error_when_no_device_could_be_archived()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(vendorId, deviceId, "device.xml");

        var request = new DownloadIoddRequest { VendorId = vendorId, DeviceIds = [deviceId] };

        _ioddStore.Devices.Returns([deviceInfo]);
        _ioddStore.FillVendorArchive(Arg.Any<ZipArchive>(), Arg.Any<int>(), Arg.Any<IEnumerable<int>>())
            .Returns([]);

        // Act
        var response = await tester.TestRequest<DownloadIoddResponse, DownloadIoddRequest>(request);

        // Assert
        response.RequestError.Should().NotBeNull();
        response.RequestError.ErrorCode.Should().Be(400);
    }

    [Fact]
    public async Task Request_should_return_error_when_vendor_not_found()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var otherDevice = DeviceInfoFactory.CreateDeviceInfo(311, 3, "other.xml");

        var request = new DownloadIoddRequest { VendorId = 310, DeviceIds = [2] };

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

        var request = new DownloadIoddRequest { VendorId = 310, DeviceIds = [2] };

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

        var request = new DownloadIoddRequest { VendorId = vendorId, DeviceIds = [deviceId] };

        _ioddStore.Devices.Returns([deviceInfo]);
        FillArchiveWith(($"{vendorId}/{deviceId}/file.xml", "content"));

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
        var testContent = "test iodd content";

        var request = new DownloadIoddRequest { VendorId = vendorId, DeviceIds = [deviceId] };

        _ioddStore.Devices.Returns([deviceInfo]);
        FillArchiveWith(($"{vendorId}/{deviceId}/iodd.xml", testContent));

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
