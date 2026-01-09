using ClusterManagement.Backend.Iodds.Consumers;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Iodds;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Backend.Messaging;
using Sdk.Testing.Backend;
using Xunit;

namespace ClusterManagement.Backend.Tests.Iodds.Consumers;

public class SetAutoIoddDownloadConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly ISuiteMediator _mediator = Substitute.For<ISuiteMediator>();
    private readonly IIoddStore _ioddStore = Substitute.For<IIoddStore>();

    public SetAutoIoddDownloadConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<SetAutoIoddDownloadConsumer>();
            cfg.AddSingleton(_mediator);
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
        await _mediator.Received(1).Publish(Arg.Is<AutoIoddDownloadChanged>(e => e.Value));
    }
}
