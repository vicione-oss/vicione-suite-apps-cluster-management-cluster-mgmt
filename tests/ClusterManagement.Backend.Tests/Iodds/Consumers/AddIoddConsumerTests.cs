using System.IO.Compression;
using ClusterManagement.Backend.Iodds.Consumers;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Commands;
using ClusterManagement.Public.Iodds.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Messaging;
using Sdk.Testing.Backend;

namespace ClusterManagement.Backend.Tests.Iodds.Consumers;

public class AddIoddConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IIoddStore _ioddStore = Substitute.For<IIoddStore>();

    public AddIoddConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<AddIoddConsumer>();
            cfg.AddSingleton(_ioddStore);
        };

    [Fact]
    public async Task Command_should_be_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var zipData = CreateValidZipArchive();
        var command = new AddIodd(zipData);
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo();

        _ioddStore.TryAddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(x =>
            {
                x[1] = new IoddId(310, 2);
                return true;
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
        var command = new AddIodd(zipData);
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo();
        var expectedId = new IoddId(310, 2);

        _ioddStore.TryAddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(x =>
            {
                x[1] = expectedId;
                return true;
            });
        _ioddStore.Devices.Returns([deviceInfo]);

        // Act
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);

        // Assert
        _ioddStore.Received(1).TryAddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>());
    }

    [Fact]
    public async Task Command_should_publish_correlated_changed_event_on_success()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var zipData = CreateValidZipArchive();
        var command = new AddIodd(zipData);
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo();
        var expectedId = new IoddId(310, 2);

        _ioddStore.TryAddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(x =>
            {
                x[1] = expectedId;
                return true;
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
    public async Task Should_publish_change_with_error_when_device_already_exists()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var zipData = CreateValidZipArchive();
        var command = new AddIodd(zipData);

        _ioddStore.TryAddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(false);

        // Act
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<IoddChanged>(k
            => k.Context.Message.Error is not null
            && k.Context.Message.CorrelationId == command.CorrelationId,
            TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_publish_change_with_error_when_TryAddDevice_fails()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var zipData = CreateValidZipArchive();
        var command = new AddIodd(zipData);

        _ioddStore.TryAddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(x =>
            {
                x[1] = new IoddId(310, 2);
                return false;
            });

        // Act
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<IoddChanged>(k
            => k.Context.Message.Error is not null
            && k.Context.Message.CorrelationId == command.CorrelationId,
            TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Should_publish_change_with_error_on_handle_invalid_zip_data()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var invalidZipData = new byte[] { 0x00, 0x01, 0x02 };
        var command = new AddIodd(invalidZipData);

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
        var command = new AddIodd(emptyZipData);

        _ioddStore.TryAddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(false);

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
        var command = new AddIodd(zipData);
        var deviceInfo1 = DeviceInfoFactory.CreateDeviceInfo(310, 2);
        var deviceInfo2 = DeviceInfoFactory.CreateDeviceInfo(311, 3);
        var expectedId = new IoddId(310, 2);

        _ioddStore.TryAddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(x =>
            {
                x[1] = expectedId;
                return true;
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
        var command = new AddIodd(zipData);
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo();

        _ioddStore.TryAddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>())
            .Returns(x =>
            {
                x[1] = new IoddId(310, 2);
                return true;
            });
        _ioddStore.Devices.Returns([deviceInfo]);

        // Act
        await tester.TestCommand<AddIodd, AddIoddConsumer>(command);

        // Assert - Should complete without resource leaks
        _ioddStore.Received(1).TryAddDevice(Arg.Any<ZipArchive>(), out Arg.Any<IoddId?>());
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
