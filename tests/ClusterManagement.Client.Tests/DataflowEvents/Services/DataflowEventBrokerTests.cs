using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Public.DataflowEvents;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.Logging;
using MQTTnet;
using Sdk.Client.Infrastructure;

namespace ClusterManagement.Client.Tests.DataflowEvents.Services;

public class DataflowEventBrokerTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly IDataflowEventMqttHandler _mqttHandler = Substitute.For<IDataflowEventMqttHandler>();
    private readonly ILogger<DataflowEventBroker> _logger = Substitute.For<ILogger<DataflowEventBroker>>();
    private readonly Func<DateTime, string?, Task> _handler = Substitute.For<Func<DateTime, string?, Task>>();

    private readonly IDataflowEvent _inputEvent = DataflowEventTestUtils.CreateInputEvent();
    private readonly IDataflowEvent _outputEvent = DataflowEventTestUtils.CreateOutputEvent();

    private DataflowEventBroker CreateEventBroker(params IDataflowEvent[] clusterEvents)
    {
        _mediator.SetupDataflowEventRequest(clusterEvents);
        _mediator.Request<GetClusterInfosRequest, GetClusterInfosResponse>(Arg.Any<GetClusterInfosRequest>(), CancellationToken.None)
                .Returns(new GetClusterInfosResponse()
                {
                    Results = [new ClusterInfo(Guid.NewGuid(), new Version(0, 1, 5), "Cluster", true)],
                });

        return new(_mediator, _mqttHandler, _logger);
    }

    public class GetEvents : DataflowEventBrokerTests
    {
        [Fact]
        public async Task Should_load_cluster_events_on_first_call()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);

            // Act
            var events = await broker.GetEvents();

            // Assert
            events.Should().ContainSingle(k => k.CanWrite, "input");
            events.Should().ContainSingle(k => k.CanRead, "output");
        }
    }

    public class Subscribe : DataflowEventBrokerTests
    {
        [Fact]
        public async Task Should_load_cluster_events_if_not_cached()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);

            // Act
            var subscription = await broker.Subscribe(_outputEvent.EventId, _handler);

            // Assert
            subscription.Should().NotBeNull();
        }

        [Fact]
        public async Task Should_subscribe_event_for_first_subscription()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);

            // Act
            _ = await broker.Subscribe(_outputEvent.EventId, _handler);

            // Assert
            await _mqttHandler.ReceivedWithAnyArgs(1).Subscribe(default!, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_subscribe_event_for_several_subscriptions()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);

            // Act
            _ = await broker.Subscribe(_outputEvent.EventId, _handler);
            _ = await broker.Subscribe(_outputEvent.EventId, _handler);

            // Assert
            await _mqttHandler.ReceivedWithAnyArgs(1).Subscribe(default!, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Value_changed_should_be_called_for_later_subscriptions_with_already_received_data()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var fakeEvent = CreateEventArgs(_outputEvent.EventId, "[some json data]");
            await using var subscription1 = await broker.Subscribe(_outputEvent.EventId, _handler);
            _mqttHandler.EventReceivedAsync += Raise.Event<Func<DataflowEventArgs, Task>>(fakeEvent);
            var valueChanged = false;

            // Act
            await using var subscription2 = await broker.Subscribe(_outputEvent.EventId, (_, _) => { valueChanged = true; return Task.CompletedTask; });

            // Assert
            valueChanged.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("[some json data]")]
        public async Task Value_changed_should_be_called_on_payload_received(string? payload)
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var fakeEvent = CreateEventArgs(_outputEvent.EventId, payload);
            var valueChanged = false;
            await using var subscription = await broker.Subscribe(_outputEvent.EventId, (_, _) => { valueChanged = true; return Task.CompletedTask; });

            // Act
            _mqttHandler.EventReceivedAsync += Raise.Event<Func<DataflowEventArgs, Task>>(fakeEvent);

            // Assert
            valueChanged.Should().BeTrue();
        }

        [Fact]
        public async Task Value_changed_should_be_called_on_multiple_handles_on_valid_payload_received()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var fakeEvent = CreateEventArgs(_outputEvent.EventId, "[some json data]");
            var valueChanged1 = false;
            var valueChanged2 = false;
            await using var subscription1 = await broker.Subscribe(_outputEvent.EventId, (_, _) => { valueChanged1 = true; return Task.CompletedTask; });
            await using var subscription2 = await broker.Subscribe(_outputEvent.EventId, (_, _) => { valueChanged2 = true; return Task.CompletedTask; });

            // Act
            _mqttHandler.EventReceivedAsync += Raise.Event<Func<DataflowEventArgs, Task>>(fakeEvent);

            // Assert
            valueChanged1.Should().BeTrue();
            valueChanged2.Should().BeTrue();
        }

        private static DataflowEventArgs CreateEventArgs(Guid eventId, string? payload)
        {
            var msgBuilder = new MqttApplicationMessageBuilder()
                .WithTopic(eventId.ToString())
                .WithPayload(payload);

            return new DataflowEventArgs(eventId, msgBuilder.Build(), DateTime.UtcNow);
        }
    }

    public class Unsubscribe : DataflowEventBrokerTests
    {
        [Fact]
        public async Task Should_not_unsubscribe_event_if_subscription_is_remaining()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var subscription1 = await broker.Subscribe(_outputEvent.EventId, _handler);
            _ = await broker.Subscribe(_outputEvent.EventId, _handler);

            // Act
            await subscription1.DisposeAsync();

            // Assert
            await _mqttHandler.DidNotReceiveWithAnyArgs().Unsubscribe(default!, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_unsubscribe_event_if_no_subscription_is_left()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var subscription1 = await broker.Subscribe(_outputEvent.EventId, _handler);
            var subscription2 = await broker.Subscribe(_outputEvent.EventId, _handler);

            // Act
            await subscription1.DisposeAsync();
            await subscription2.DisposeAsync();

            // Assert
            await _mqttHandler.ReceivedWithAnyArgs(1).Unsubscribe(default!, Arg.Any<CancellationToken>());
        }
    }

    public class SetValue : DataflowEventBrokerTests
    {
        [Fact]
        public async Task Should_call_publish_value_on_mqtt_handler_for_existing_handle()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            var value = "[some data]";

            // Act
            await broker.SetValue(_inputEvent.EventId, value, TestContext.Current.CancellationToken);

            // Assert
            await _mqttHandler.Received().PublishValue(_inputEvent.EventId, value, _inputEvent.ValueType, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_for_non_writable_event()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);

            // Act
            var action = () => broker.SetValue(_outputEvent.EventId, "value", TestContext.Current.CancellationToken);

            // Assert
            await action.Should().ThrowAsync<ArgumentException>().WithMessage("*not writable*");
        }

        [Fact]
        public async Task Should_throw_for_unknown_event()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);

            // Act
            var action = () => broker.SetValue(Guid.NewGuid(), "value", TestContext.Current.CancellationToken);

            // Assert
            await action.Should().ThrowAsync<ArgumentException>().WithMessage("*does not exist*");
        }
    }

    public class SubscribeNegativePaths : DataflowEventBrokerTests
    {
        [Fact]
        public async Task Should_throw_for_non_readable_event()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);

            // Act
            var action = () => broker.Subscribe(_inputEvent.EventId, _handler);

            // Assert
            await action.Should().ThrowAsync<ArgumentException>().WithMessage("*not readable*");
        }

        [Fact]
        public async Task Should_throw_for_unknown_event()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);

            // Act
            var action = () => broker.Subscribe(Guid.NewGuid(), _handler);

            // Assert
            await action.Should().ThrowAsync<ArgumentException>().WithMessage("*does not exist*");
        }
    }

    public class ConsumeClusterUpdate : DataflowEventBrokerTests
    {
        [Fact]
        public async Task Should_invalidate_cache_and_trigger_reconnect()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            _ = await broker.GetEvents(); // prime cache
            var statusEvent = new ClusterUpdateCompleted(Guid.NewGuid(), Guid.NewGuid(), new Version(0, 1, 0), TimeSpan.FromSeconds(1));
            var context = new ClientContext<ClusterUpdateCompleted>(statusEvent, Guid.NewGuid());

            // Act
            await broker.Consume(context, CancellationToken.None);

            // Assert — reconnect is triggered on the mqtt handler
            await _mqttHandler.Received(1).ResetAndReconnect(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_reload_events_after_cache_invalidation()
        {
            // Arrange
            var broker = CreateEventBroker(_inputEvent, _outputEvent);
            _ = await broker.GetEvents(); // prime cache
            var statusEvent = new ClusterUpdateCompleted(Guid.NewGuid(), Guid.NewGuid(), new Version(0, 1, 0), TimeSpan.FromSeconds(1));
            var context = new ClientContext<ClusterUpdateCompleted>(statusEvent, Guid.NewGuid());

            // Act — Consume fires reconnect; since mock returns synchronously, task completes immediately
            await broker.Consume(context, CancellationToken.None);
            var events = await broker.GetEvents();

            // Assert — mediator should have been called again (2 total: initial + after invalidation)
            await _mediator.Received(2).Request<GetDataflowEventsRequest, GetDataflowEventsResponse>(Arg.Any<GetDataflowEventsRequest>(), Arg.Any<CancellationToken>());
        }
    }
}
