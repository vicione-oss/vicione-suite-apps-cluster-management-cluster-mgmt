using AwesomeAssertions;
using ClusterManagement.Backend.Iodds.Consumers;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Iodds;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Backend.Messaging;
using Sdk.Messaging;
using Sdk.Testing.Backend;
using Xunit;

namespace ClusterManagement.Backend.Tests.Iodds.Consumers;

public class DeleteIoddConsumerTests
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IIoddStore _ioddStore = Substitute.For<IIoddStore>();
    private readonly ISuiteMediator _mediator = Substitute.For<ISuiteMediator>();

    public DeleteIoddConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<DeleteIoddConsumer>();
            cfg.AddSingleton(_ioddStore);
            cfg.AddSingleton(_mediator);
        };

    [Fact]
    public async Task Command_should_be_consumed()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteIodd(310, 2);

        _ioddStore.TryRemoveDevice(Arg.Any<IoddId>()).Returns(true);

        // Act + Assert
        await tester.TestCommand<DeleteIodd, DeleteIoddConsumer>(command);
    }

    [Fact]
    public async Task Command_should_delete_device_successfully()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var command = new DeleteIodd(vendorId, deviceId);

        _ioddStore.TryRemoveDevice(Arg.Any<IoddId>()).Returns(true);

        // Act
        await tester.TestCommand<DeleteIodd, DeleteIoddConsumer>(command);

        // Assert
        _ioddStore.Received(1).TryRemoveDevice(Arg.Is<IoddId>(id =>
            id.VendorId == vendorId && id.DeviceId == deviceId));
    }

    [Fact]
    public async Task Command_should_publish_IoddChanged_event_on_success()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var command = new DeleteIodd(vendorId, deviceId);

        _ioddStore.TryRemoveDevice(Arg.Any<IoddId>()).Returns(true);

        // Act
        await tester.TestCommand<DeleteIodd, DeleteIoddConsumer>(command);

        // Assert
        await _mediator.Received(1).Publish(Arg.Is<IoddChanged>(e =>
            e.Iodd.VendorId == vendorId &&
            e.Iodd.DeviceId == deviceId &&
            e.Action == CrudAction.Deleted &&
            e.DeviceInfo == null));
    }

    [Fact]
    public async Task Command_should_publish_error_event_when_device_does_not_exist()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 310;
        var deviceId = 2;
        var command = new DeleteIodd(vendorId, deviceId);

        _ioddStore.TryRemoveDevice(Arg.Any<IoddId>()).Returns(false);

        // Act
        await tester.TestCommand<DeleteIodd, DeleteIoddConsumer>(command);

        // Assert
        await _mediator.Received(1).Publish(Arg.Is<IoddError>(e =>
            e.Error.ErrorCode == IoddError.DeleteIoddError &&
            e.Name == "Delete Failed"));
    }

    [Fact]
    public async Task Command_should_publish_error_event_when_deletion_fails()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteIodd(310, 2);

        _ioddStore.TryRemoveDevice(Arg.Any<IoddId>()).Returns(false);

        // Act
        await tester.TestCommand<DeleteIodd, DeleteIoddConsumer>(command);

        // Assert
        await _mediator.Received(1).Publish(Arg.Is<IoddError>(e =>
            e.Error.ErrorCode == IoddError.DeleteIoddError &&
            e.Error.Message == "Failed to delete IODD"));
    }

    [Fact]
    public async Task Command_should_not_publish_changed_event_when_deletion_fails()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteIodd(310, 2);

        _ioddStore.TryRemoveDevice(Arg.Any<IoddId>()).Returns(false);

        // Act
        await tester.TestCommand<DeleteIodd, DeleteIoddConsumer>(command);

        // Assert
        await _mediator.DidNotReceive().Publish(Arg.Any<IoddChanged>());
    }

    [Fact]
    public async Task Command_should_create_correct_id()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var vendorId = 999;
        var deviceId = 888;
        var command = new DeleteIodd(vendorId, deviceId);

        _ioddStore.TryRemoveDevice(Arg.Any<IoddId>()).Returns(true);

        // Act
        await tester.TestCommand<DeleteIodd, DeleteIoddConsumer>(command);

        // Assert
        _ioddStore.Received(1).TryRemoveDevice(Arg.Is<IoddId>(id =>
            id.VendorId == vendorId && id.DeviceId == deviceId));
    }

    [Fact]
    public async Task Command_should_handle_zero_ids()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteIodd(0, 0);

        _ioddStore.TryRemoveDevice(Arg.Any<IoddId>()).Returns(true);

        // Act
        await tester.TestCommand<DeleteIodd, DeleteIoddConsumer>(command);

        // Assert
        _ioddStore.Received(1).TryRemoveDevice(Arg.Is<IoddId>(id =>
            id.VendorId == 0 && id.DeviceId == 0));
    }

    [Fact]
    public async Task Command_should_publish_only_one_event_per_deletion()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteIodd(310, 2);

        _ioddStore.TryRemoveDevice(Arg.Any<IoddId>()).Returns(true);

        // Act
        await tester.TestCommand<DeleteIodd, DeleteIoddConsumer>(command);

        // Assert
        await _mediator.Received(1).Publish(Arg.Any<IoddChanged>());
        await _mediator.DidNotReceive().Publish(Arg.Any<IoddError>());
    }

    [Fact]
    public async Task Command_should_publish_error_with_correct_error_code()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new DeleteIodd(310, 2);

        _ioddStore.TryRemoveDevice(Arg.Any<IoddId>()).Returns(false);

        // Act
        await tester.TestCommand<DeleteIodd, DeleteIoddConsumer>(command);

        // Assert
        await _mediator.Received(1).Publish(Arg.Is<IoddError>(e =>
            e.Error.ErrorCode == IoddError.DeleteIoddError));
    }

    [Fact]
    public async Task Command_should_handle_exception_from_store()
    {
        // Arrange
        var exceptionConfigureServices = new Action<IBusRegistrationConfigurator>(cfg =>
        {
            cfg.AddConsumer<DeleteIoddConsumer>();
            cfg.AddSingleton(_mediator);
            cfg.AddSingleton<IIoddStore>(_ =>
            {
                var store = Substitute.For<IIoddStore>();
                store.TryRemoveDevice(Arg.Any<IoddId>())
                    .Returns(_ => throw new InvalidOperationException("Store error"));
                return store;
            });
        });

        await using var tester = new MassTransitTester(exceptionConfigureServices);
        var command = new DeleteIodd(310, 2);

        // Act
        var act = async () => await tester.TestCommand<DeleteIodd, DeleteIoddConsumer>(command);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Store error");
    }
}
