using ClusterManagement.Backend.Iodds.Consumers;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;

namespace ClusterManagement.Backend.Tests.Iodds.Consumers;

public class SetAutoIoddDownloadConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IIoddStore _ioddStore = Substitute.For<IIoddStore>();

    public SetAutoIoddDownloadConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<SetAutoIoddDownloadConsumer>();
            cfg.AddSingleton(_ioddStore);
        };

    [Fact]
    public async Task Command_should_enable_auto_download()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new SetAutoIoddDownload(true);

        // Act
        await tester.TestCommand<SetAutoIoddDownload, SetAutoIoddDownloadConsumer>(command);

        // Assert
        await _ioddStore.Received(1).SetAutoDownloadIodds(true);
    }

    [Fact]
    public async Task Command_should_disable_auto_download()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new SetAutoIoddDownload(false);

        // Act
        await tester.TestCommand<SetAutoIoddDownload, SetAutoIoddDownloadConsumer>(command);

        // Assert
        await _ioddStore.Received(1).SetAutoDownloadIodds(false);
    }

    [Fact]
    public async Task Command_should_publish_changed_event()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new SetAutoIoddDownload(true);

        // Act
        await tester.TestCommand<SetAutoIoddDownload, SetAutoIoddDownloadConsumer>(command);

        // Assert
        (await tester.Harness.Published.Any<AutoIoddDownloadChanged>(TestContext.Current.CancellationToken)).Should().BeTrue();
    }
}
