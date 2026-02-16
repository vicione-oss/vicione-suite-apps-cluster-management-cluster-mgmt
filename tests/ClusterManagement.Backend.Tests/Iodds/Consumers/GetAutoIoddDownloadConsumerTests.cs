using ClusterManagement.Backend.Iodds.Consumers;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Iodds;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;

namespace ClusterManagement.Backend.Tests.Iodds.Consumers;

public class GetAutoIoddDownloadConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IIoddStore _ioddStore = Substitute.For<IIoddStore>();

    public GetAutoIoddDownloadConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetAutoIoddDownloadConsumer>();
            cfg.AddSingleton<IIoddStore>(_ioddStore);
        };

    [Fact]
    public async Task Request_should_return_true_when_auto_download_enabled()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetAutoIoddDownloadRequest();

        _ioddStore.GetAutoDownloadIodds().Returns(true);

        // Act
        var response = await tester.TestRequest<GetAutoIoddDownloadResponse, GetAutoIoddDownloadRequest>(request);

        // Assert
        response.IsAutoDownload.Should().BeTrue();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_return_false_when_auto_download_disabled()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetAutoIoddDownloadRequest();

        _ioddStore.GetAutoDownloadIodds().Returns(false);

        // Act
        var response = await tester.TestRequest<GetAutoIoddDownloadResponse, GetAutoIoddDownloadRequest>(request);

        // Assert
        response.IsAutoDownload.Should().BeFalse();
        response.RequestError.Should().BeNull();
    }

    [Fact]
    public async Task Request_should_handle_exception_gracefully()
    {
        // Arrange
        var failingConfigureServices = new Action<IBusRegistrationConfigurator>(cfg =>
        {
            cfg.AddConsumer<GetAutoIoddDownloadConsumer>();
            cfg.AddSingleton<IIoddStore>(_ =>
            {
                var store = Substitute.For<IIoddStore>();
                store.GetAutoDownloadIodds().Returns(_ => throw new InvalidOperationException("Database error"));
                return store;
            });
        });

        await using var tester = new MassTransitTester(failingConfigureServices);
        var request = new GetAutoIoddDownloadRequest();

        // Act
        var response = await tester.TestRequest<GetAutoIoddDownloadResponse, GetAutoIoddDownloadRequest>(request);

        // Assert
        response.RequestError.Should().NotBeNull();
        response.RequestError.ErrorCode.Should().Be(500);
    }
}
