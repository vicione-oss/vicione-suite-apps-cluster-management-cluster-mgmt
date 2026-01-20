using AwesomeAssertions;
using ClusterManagement.Backend.Iodds.Consumers;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace ClusterManagement.Backend.Tests.Iodds.Consumers;

public class GetIODevicesConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IIoddStore _ioddStore = Substitute.For<IIoddStore>();

    public GetIODevicesConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetIODevicesConsumer>();
            cfg.AddSingleton(_ioddStore);
        };

    [Fact]
    public async Task Request_should_return_empty_collection_when_no_devices_exist()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetIODevicesRequest();

        _ioddStore.Devices.Returns([]);

        // Act
        var response = await tester.TestRequest<GetIODevicesResponse, GetIODevicesRequest>(request);

        // Assert
        response.IODevices.Should().BeEmpty();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_return_single_device()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetIODevicesRequest();
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(310, 2);

        _ioddStore.Devices.Returns([deviceInfo]);

        // Act
        var response = await tester.TestRequest<GetIODevicesResponse, GetIODevicesRequest>(request);

        // Assert
        response.IODevices.Should().HaveCount(1);
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_return_multiple_devices()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetIODevicesRequest();

        _ioddStore.Devices.Returns([
            DeviceInfoFactory.CreateDeviceInfo(310, 2),
            DeviceInfoFactory.CreateDeviceInfo(311, 3),
            DeviceInfoFactory.CreateDeviceInfo(312, 4)]
            );

        // Act
        var response = await tester.TestRequest<GetIODevicesResponse, GetIODevicesRequest>(request);

        // Assert
        response.IODevices.Should().HaveCount(3);
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_return_all_devices_from_store()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetIODevicesRequest();
        var devices = new List<DeviceInfo>
        {
            DeviceInfoFactory.CreateDeviceInfo(310, 2),
            DeviceInfoFactory.CreateDeviceInfo(311, 3),
            DeviceInfoFactory.CreateDeviceInfo(312, 4),
        };

        _ioddStore.Devices.Returns(devices);

        // Act
        var response = await tester.TestRequest<GetIODevicesResponse, GetIODevicesRequest>(request);

        // Assert
        response.IODevices.Should().HaveCount(3);
        response.IODevices.Should().BeEquivalentTo(devices);
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_handle_exception_gracefully()
    {
        // Arrange
        var failingConfigureServices = new Action<IBusRegistrationConfigurator>(cfg =>
        {
            cfg.AddConsumer<GetIODevicesConsumer>();
            cfg.AddSingleton<IIoddStore>(_ =>
            {
                var store = Substitute.For<IIoddStore>();
                store.Devices.Returns(_ => throw new InvalidOperationException("Store error"));
                return store;
            });
        });

        await using var tester = new MassTransitTester(failingConfigureServices);
        var request = new GetIODevicesRequest();

        // Act
        var response = await tester.TestRequest<GetIODevicesResponse, GetIODevicesRequest>(request);

        // Assert
        response.RequestError.Should().NotBeNull();
        response.RequestError.ErrorCode.Should().Be(500);
        response.IODevices.Should().BeEmpty();
    }

    [Fact]
    public async Task Request_should_return_devices_with_correct_properties()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetIODevicesRequest();
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(310, 2);

        _ioddStore.Devices.Returns([deviceInfo]);

        // Act
        var response = await tester.TestRequest<GetIODevicesResponse, GetIODevicesRequest>(request);

        // Assert
        var returnedDevice = response.IODevices.Should().ContainSingle().Which;
        returnedDevice.DeviceDescription.DeviceIdentity.VendorId.Should().Be(310);
        returnedDevice.DeviceDescription.DeviceIdentity.DeviceId.Should().Be(2);
        returnedDevice.Filename.Should().NotBeNullOrEmpty();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_handle_store_returning_null_safely()
    {
        // Arrange
        var nullConfigureServices = new Action<IBusRegistrationConfigurator>(cfg =>
        {
            cfg.AddConsumer<GetIODevicesConsumer>();
            cfg.AddSingleton<IIoddStore>(_ =>
            {
                var store = Substitute.For<IIoddStore>();
                store.Devices.Returns((IReadOnlyCollection<DeviceInfo>)null!);
                return store;
            });
        });

        await using var tester = new MassTransitTester(nullConfigureServices);
        var request = new GetIODevicesRequest();

        // Act
        var response = await tester.TestRequest<GetIODevicesResponse, GetIODevicesRequest>(request);

        // Assert
        response.IODevices.Should().BeNull();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_access_store_devices_once()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetIODevicesRequest();
        var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(310, 2);

        _ioddStore.Devices.Returns([deviceInfo]);

        // Act
        await tester.TestRequest<GetIODevicesResponse, GetIODevicesRequest>(request);

        // Assert
        _ = _ioddStore.Received(1).Devices;
    }

    [Fact]
    public async Task Request_should_handle_multiple_requests_independently()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request1 = new GetIODevicesRequest();
        var request2 = new GetIODevicesRequest();

        var devices1 = new List<DeviceInfo> { DeviceInfoFactory.CreateDeviceInfo(310, 2) };
        var devices2 = new List<DeviceInfo> { DeviceInfoFactory.CreateDeviceInfo(311, 3), DeviceInfoFactory.CreateDeviceInfo(312, 4) };

        _ioddStore.Devices.Returns(devices1, devices2);

        // Act
        var response1 = await tester.TestRequest<GetIODevicesResponse, GetIODevicesRequest>(request1);
        var response2 = await tester.TestRequest<GetIODevicesResponse, GetIODevicesRequest>(request2);

        // Assert
        response1.IODevices.Should().HaveCount(1);
        response2.IODevices.Should().HaveCount(2);
        response1.RequestError.Should().BeNull();
        response2.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_return_error_with_exception_details()
    {
        // Arrange
        var exceptionMessage = "Database connection failed";
        var failingConfigureServices = new Action<IBusRegistrationConfigurator>(cfg =>
        {
            cfg.AddConsumer<GetIODevicesConsumer>();
            cfg.AddSingleton<IIoddStore>(_ =>
            {
                var store = Substitute.For<IIoddStore>();
                store.Devices.Returns(_ => throw new InvalidOperationException(exceptionMessage));
                return store;
            });
        });

        await using var tester = new MassTransitTester(failingConfigureServices);
        var request = new GetIODevicesRequest();

        // Act
        var response = await tester.TestRequest<GetIODevicesResponse, GetIODevicesRequest>(request);

        // Assert
        response.RequestError.Should().NotBeNull();
    }
}
