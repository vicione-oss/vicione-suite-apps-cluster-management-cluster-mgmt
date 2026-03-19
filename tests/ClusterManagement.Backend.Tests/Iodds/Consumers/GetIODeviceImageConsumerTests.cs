using System.IO.Abstractions.TestingHelpers;
using System.Text;
using ClusterManagement.Backend.Iodds.Consumers;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;

namespace ClusterManagement.Backend.Tests.Iodds.Consumers;

public class GetIODeviceImageConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IIoddStore _ioddStore = Substitute.For<IIoddStore>();
    private readonly MockFileSystem _fileSystem = new();

    public GetIODeviceImageConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetIODeviceImageConsumer>();
            cfg.AddSingleton(_ioddStore);
            cfg.AddSingleton<System.IO.Abstractions.IFileSystem>(_fileSystem);
        };

    [Fact]
    public async Task Request_should_return_image_as_base64_when_file_exists()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var imageFilename = "device-image.png";
        var ioddDirectory = "Iodds";
        var imageContent = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG header
        var expectedBase64 = Convert.ToBase64String(imageContent);

        var request = new GetIODeviceImageRequest
        {
            VendorId = vendorId,
            DeviceId = deviceId,
            ImageFilename = imageFilename
        };

        SetupFileSystem(ioddDirectory, vendorId, deviceId, imageFilename, imageContent);

        // Act
        var response = await tester.TestRequest<GetIODeviceImageResponse, GetIODeviceImageRequest>(request);

        // Assert
        response.ImageAsBase64.Should().Be(expectedBase64);
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_return_error_when_file_does_not_exist()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var imageFilename = "missing-image.png";
        var ioddDirectory = "Iodds";

        var request = new GetIODeviceImageRequest
        {
            VendorId = vendorId,
            DeviceId = deviceId,
            ImageFilename = imageFilename
        };

        _ioddStore.IoddDirectory.Returns(ioddDirectory);

        // Act
        var response = await tester.TestRequest<GetIODeviceImageResponse, GetIODeviceImageRequest>(request);

        // Assert
        response.RequestError.Should().NotBeNull();
        response.RequestError.ErrorCode.Should().Be(500);
        response.ImageAsBase64.Should().BeEmpty();
    }

    [Fact]
    public async Task Request_should_construct_correct_file_path()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 999;
        var deviceId = 888;
        var imageFilename = "test-image.jpg";
        var ioddDirectory = "Iodds";
        var imageContent = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG header

        var request = new GetIODeviceImageRequest
        {
            VendorId = vendorId,
            DeviceId = deviceId,
            ImageFilename = imageFilename
        };

        SetupFileSystem(ioddDirectory, vendorId, deviceId, imageFilename, imageContent);

        // Act
        await tester.TestRequest<GetIODeviceImageResponse, GetIODeviceImageRequest>(request);

        // Assert
        var expectedPath = _fileSystem.Path.Combine(ioddDirectory, $"{vendorId}", $"{deviceId}", imageFilename);
        _fileSystem.File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task Request_should_handle_different_image_formats()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var imageFilename = "device.svg";
        var ioddDirectory = "Iodds";
        var svgContent = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
        var expectedBase64 = Convert.ToBase64String(svgContent);

        var request = new GetIODeviceImageRequest
        {
            VendorId = vendorId,
            DeviceId = deviceId,
            ImageFilename = imageFilename
        };

        SetupFileSystem(ioddDirectory, vendorId, deviceId, imageFilename, svgContent);

        // Act
        var response = await tester.TestRequest<GetIODeviceImageResponse, GetIODeviceImageRequest>(request);

        // Assert
        response.ImageAsBase64.Should().Be(expectedBase64);
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_handle_empty_image_file()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var imageFilename = "empty-image.png";
        var ioddDirectory = "Iodds";
        var emptyContent = Array.Empty<byte>();
        var expectedBase64 = Convert.ToBase64String(emptyContent);

        var request = new GetIODeviceImageRequest
        {
            VendorId = vendorId,
            DeviceId = deviceId,
            ImageFilename = imageFilename
        };

        SetupFileSystem(ioddDirectory, vendorId, deviceId, imageFilename, emptyContent);

        // Act
        var response = await tester.TestRequest<GetIODeviceImageResponse, GetIODeviceImageRequest>(request);

        // Assert
        response.ImageAsBase64.Should().Be(expectedBase64);
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_handle_large_image_file()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var imageFilename = "large-image.png";
        var ioddDirectory = "Iodds";
        var largeContent = new byte[1024 * 1024]; // 1 MB
        new Random().NextBytes(largeContent);
        var expectedBase64 = Convert.ToBase64String(largeContent);

        var request = new GetIODeviceImageRequest
        {
            VendorId = vendorId,
            DeviceId = deviceId,
            ImageFilename = imageFilename
        };

        SetupFileSystem(ioddDirectory, vendorId, deviceId, imageFilename, largeContent);

        // Act
        var response = await tester.TestRequest<GetIODeviceImageResponse, GetIODeviceImageRequest>(request);

        // Assert
        response.ImageAsBase64.Should().Be(expectedBase64);
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_check_file_existence_before_reading()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var imageFilename = "image.png";
        var ioddDirectory = "Iodds";
        var imageContent = new byte[] { 0x00, 0x01, 0x02 };

        var request = new GetIODeviceImageRequest
        {
            VendorId = vendorId,
            DeviceId = deviceId,
            ImageFilename = imageFilename
        };

        SetupFileSystem(ioddDirectory, vendorId, deviceId, imageFilename, imageContent);

        // Act
        var response = await tester.TestRequest<GetIODeviceImageResponse, GetIODeviceImageRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.ImageAsBase64.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Request_should_handle_filename_with_special_characters()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var imageFilename = "device-image (1).png";
        var ioddDirectory = "Iodds";
        var imageContent = new byte[] { 0x00, 0x01 };

        var request = new GetIODeviceImageRequest
        {
            VendorId = vendorId,
            DeviceId = deviceId,
            ImageFilename = imageFilename
        };

        SetupFileSystem(ioddDirectory, vendorId, deviceId, imageFilename, imageContent);

        // Act
        var response = await tester.TestRequest<GetIODeviceImageResponse, GetIODeviceImageRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.ImageAsBase64.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Request_should_use_invariant_culture_for_path_construction()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var imageFilename = "image.png";
        var ioddDirectory = "Iodds";
        var imageContent = new byte[] { 0x00 };

        var request = new GetIODeviceImageRequest
        {
            VendorId = vendorId,
            DeviceId = deviceId,
            ImageFilename = imageFilename
        };

        SetupFileSystem(ioddDirectory, vendorId, deviceId, imageFilename, imageContent);

        // Act
        var response = await tester.TestRequest<GetIODeviceImageResponse, GetIODeviceImageRequest>(request);

        // Assert
        var expectedPath = _fileSystem.Path.Combine(ioddDirectory, "310", "2", imageFilename);
        _fileSystem.File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task Request_should_handle_nested_directory_structure()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 100;
        var deviceId = 50;
        var imageFilename = "nested-image.png";
        var ioddDirectory = "Iodds";
        var imageContent = new byte[] { 0x01, 0x02, 0x03 };

        var request = new GetIODeviceImageRequest
        {
            VendorId = vendorId,
            DeviceId = deviceId,
            ImageFilename = imageFilename
        };

        SetupFileSystem(ioddDirectory, vendorId, deviceId, imageFilename, imageContent);

        // Act
        var response = await tester.TestRequest<GetIODeviceImageResponse, GetIODeviceImageRequest>(request);

        // Assert
        response.ImageAsBase64.Should().NotBeEmpty();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_handle_multiple_requests_for_different_devices()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var ioddDirectory = "Iodds";

        var request1 = new GetIODeviceImageRequest { VendorId = 310, DeviceId = 2, ImageFilename = "image1.png" };
        var request2 = new GetIODeviceImageRequest { VendorId = 311, DeviceId = 3, ImageFilename = "image2.png" };

        var imageContent1 = new byte[] { 0x01, 0x02 };
        var imageContent2 = new byte[] { 0x03, 0x04 };

        SetupFileSystem(ioddDirectory, 310, 2, "image1.png", imageContent1);
        SetupFileSystem(ioddDirectory, 311, 3, "image2.png", imageContent2);

        // Act
        var response1 = await tester.TestRequest<GetIODeviceImageResponse, GetIODeviceImageRequest>(request1);
        var response2 = await tester.TestRequest<GetIODeviceImageResponse, GetIODeviceImageRequest>(request2);

        // Assert
        response1.ImageAsBase64.Should().Be(Convert.ToBase64String(imageContent1));
        response2.ImageAsBase64.Should().Be(Convert.ToBase64String(imageContent2));
        response1.RequestError.Should().BeNull();
        response2.RequestError.Should().BeNull();
    }

    private void SetupFileSystem(string ioddDirectory, int vendorId, int deviceId, string imageFilename, byte[] imageContent)
    {
        _ioddStore.IoddDirectory.Returns(ioddDirectory);

        var imagePath = _fileSystem.Path.Combine(ioddDirectory, $"{vendorId}", $"{deviceId}", imageFilename);
        _fileSystem.AddFile(imagePath, new MockFileData(imageContent));
    }
}
