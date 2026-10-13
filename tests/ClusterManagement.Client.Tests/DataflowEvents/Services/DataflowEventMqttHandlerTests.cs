using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Client.Management.Extensions;
using ClusterManagement.Public.DataflowEvents;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Packets;
using static ClusterManagement.Client.DataflowEvents.Services.DataflowEventMqttHandler;

namespace ClusterManagement.Client.Tests.DataflowEvents.Services;

public class DataflowEventMqttHandlerTests
{
    private readonly IDataflowEventMqttClientProvider _mqttClientProvider = Substitute.For<IDataflowEventMqttClientProvider>();
    private readonly IMqttClient _mqttClient = Substitute.For<IMqttClient>();
    private readonly ILogger<DataflowEventMqttHandler> _logger = Substitute.For<ILogger<DataflowEventMqttHandler>>();

    private readonly IDataflowEvent _inputEvent = DataflowEventTestUtils.CreateInputEvent();
    private readonly IDataflowEvent _outputEvent = DataflowEventTestUtils.CreateOutputEvent();

    private DataflowEventMqttHandler CreateHandler(bool isMqttGood = true)
    {
        _mqttClientProvider.CreateMqttClient().Returns(_mqttClient);

        if (isMqttGood)
            _mqttClient.ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new MqttClientConnectResult()));

        return new DataflowEventMqttHandler(_mqttClientProvider, _logger);
    }

    public class Subscribe : DataflowEventMqttHandlerTests
    {
        [Fact]
        public async Task Should_throw_if_connection_fails()
        {
            // Arrange
            await using var handler = CreateHandler(false);
            var action = () => handler.Subscribe([_outputEvent]);

            // Act + Assert
            await action.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_ensure_connection_is_established()
        {
            // Arrange
            await using var handler = CreateHandler();

            // Act
            await handler.Subscribe([_outputEvent], TestContext.Current.CancellationToken);

            // Assert
            await _mqttClient.Received().ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_connect_if_already_connected()
        {
            // Arrange
            await using var handler = CreateHandler();
            _mqttClient.IsConnected.Returns(true);

            // Act
            await handler.Subscribe([_outputEvent], TestContext.Current.CancellationToken);

            // Assert
            await _mqttClient.DidNotReceive().ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_subscribe_to_mqtt_topic()
        {
            // Arrange
            await using var handler = CreateHandler();

            // Act
            await handler.Subscribe([_outputEvent], TestContext.Current.CancellationToken);

            // Assert
            await _mqttClient.Received().SubscribeAsync(Arg.Any<MqttClientSubscribeOptions>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_subscribe_to_the_topics_of_several_events_with_one_packet()
        {
            // Arrange
            await using var handler = CreateHandler();
            var otherOutput = DataflowEventTestUtils.CreateOutputEvent("Axis", "Speed");

            // Act
            await handler.Subscribe([_outputEvent, otherOutput], TestContext.Current.CancellationToken);

            // Assert
            await _mqttClient.Received(1).SubscribeAsync(Arg.Is<MqttClientSubscribeOptions>(o =>
                o.TopicFilters.Select(f => f.Topic).SequenceEqual(new[] { _outputEvent.EventId.ToString(), otherOutput.EventId.ToString() })),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_subscribe_the_topics_of_a_failed_packet_again_after_a_reconnect()
        {
            // Arrange
            await using var handler = CreateHandler();
            _mqttClient.SubscribeAsync(Arg.Any<MqttClientSubscribeOptions>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<MqttClientSubscribeResult>(new InvalidOperationException("connection lost")), Task.FromResult<MqttClientSubscribeResult>(null!));
            var subscribe = () => handler.Subscribe([_outputEvent], TestContext.Current.CancellationToken);
            await subscribe.Should().ThrowAsync<InvalidOperationException>();

            // Act
            await handler.ResetAndReconnect(TestContext.Current.CancellationToken);

            // Assert
            await _mqttClient.Received(2).SubscribeAsync(Arg.Any<MqttClientSubscribeOptions>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_subscribe_the_topics_again_when_a_later_request_finds_the_connection_dropped()
        {
            // Arrange
            await using var handler = CreateHandler();
            await handler.Subscribe([_outputEvent], TestContext.Current.CancellationToken);

            // Act
            await handler.PublishValue(_inputEvent.EventId, "1", _inputEvent.ValueType, TestContext.Current.CancellationToken);

            // Assert
            await _mqttClient.Received(2).SubscribeAsync(Arg.Any<MqttClientSubscribeOptions>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_subscribe_every_topic_again_with_one_packet_after_a_reconnect()
        {
            // Arrange
            await using var handler = CreateHandler();
            var otherOutput = DataflowEventTestUtils.CreateOutputEvent("Axis", "Speed");
            await handler.Subscribe([_outputEvent], TestContext.Current.CancellationToken);
            await handler.Subscribe([otherOutput], TestContext.Current.CancellationToken);
            _mqttClient.ClearReceivedCalls();

            // Act
            await handler.ResetAndReconnect(TestContext.Current.CancellationToken);

            // Assert
            await _mqttClient.Received(1).SubscribeAsync(Arg.Is<MqttClientSubscribeOptions>(o =>
                o.TopicFilters.Select(f => f.Topic).Order().SequenceEqual(new[] { _outputEvent.EventId.ToString(), otherOutput.EventId.ToString() }.Order())),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_subscribe_forgotten_topics_again_after_a_reconnect()
        {
            // Arrange
            await using var handler = CreateHandler();
            _mqttClient.SubscribeAsync(Arg.Any<MqttClientSubscribeOptions>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<MqttClientSubscribeResult>(new InvalidOperationException("connection lost")));
            var subscribe = () => handler.Subscribe([_outputEvent], TestContext.Current.CancellationToken);
            await subscribe.Should().ThrowAsync<InvalidOperationException>();
            handler.Forget([_outputEvent]);

            // Act
            await handler.ResetAndReconnect(TestContext.Current.CancellationToken);

            // Assert
            await _mqttClient.Received(1).SubscribeAsync(Arg.Any<MqttClientSubscribeOptions>(), Arg.Any<CancellationToken>());
        }
    }

    public class Unsubscribe : DataflowEventMqttHandlerTests
    {
        [Fact]
        public async Task Should_throw_if_connection_fails()
        {
            // Arrange
            await using var handler = CreateHandler(false);
            var action = () => handler.Unsubscribe([_outputEvent]);

            // Act + Assert
            await action.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_ensure_connection_is_established()
        {
            // Arrange
            await using var handler = CreateHandler();

            // Act
            await handler.Unsubscribe([_outputEvent], TestContext.Current.CancellationToken);

            // Assert
            await _mqttClient.Received().ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_unsubscribe_from_mqtt_topic()
        {
            // Arrange
            await using var handler = CreateHandler();

            // Act
            await handler.Unsubscribe([_outputEvent], TestContext.Current.CancellationToken);

            // Assert
            await _mqttClient.Received().UnsubscribeAsync(Arg.Any<MqttClientUnsubscribeOptions>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_unsubscribe_from_the_topics_of_several_events_with_one_packet()
        {
            // Arrange
            await using var handler = CreateHandler();
            var otherOutput = DataflowEventTestUtils.CreateOutputEvent("Axis", "Speed");

            // Act
            await handler.Unsubscribe([_outputEvent, otherOutput], TestContext.Current.CancellationToken);

            // Assert
            await _mqttClient.Received(1).UnsubscribeAsync(Arg.Is<MqttClientUnsubscribeOptions>(o =>
                o.TopicFilters.SequenceEqual(new[] { _outputEvent.EventId.ToString(), otherOutput.EventId.ToString() })),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_go_out_only_after_an_earlier_subscribe()
        {
            // Arrange
            await using var handler = CreateHandler();
            _mqttClient.IsConnected.Returns(true);
            var subscribed = new TaskCompletionSource<MqttClientSubscribeResult>();
            _mqttClient.SubscribeAsync(Arg.Any<MqttClientSubscribeOptions>(), Arg.Any<CancellationToken>()).Returns(subscribed.Task);
            var subscribe = handler.Subscribe([_outputEvent], TestContext.Current.CancellationToken);

            // Act
            var unsubscribe = handler.Unsubscribe([_outputEvent], TestContext.Current.CancellationToken);
            var unsubscribedWhileSubscribing = _mqttClient.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IMqttClient.UnsubscribeAsync));
            subscribed.SetResult(null!);
            await Task.WhenAll(subscribe, unsubscribe);

            // Assert
            unsubscribedWhileSubscribing.Should().BeFalse();
            await _mqttClient.Received(1).UnsubscribeAsync(Arg.Any<MqttClientUnsubscribeOptions>(), Arg.Any<CancellationToken>());
        }
    }

    public class PublishValue : DataflowEventMqttHandlerTests
    {
        [Fact]
        public async Task Should_throw_if_connection_cant_be_established()
        {
            // Arrange
            await using var handler = CreateHandler(false);
            var action = () => handler.Subscribe([_outputEvent]);

            // Act + Assert
            await action.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_ensure_connection_is_established()
        {
            // Arrange
            await using var handler = CreateHandler();

            // Act
            await handler.Subscribe([_outputEvent], TestContext.Current.CancellationToken);

            // Assert
            await _mqttClient.Received().ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_publish_value_to_mqtt_broker()
        {
            // Arrange
            await using var handler = CreateHandler();

            // Act
            await handler.PublishValue(_inputEvent.EventId, "Test", typeof(string).AssemblyQualifiedName!, TestContext.Current.CancellationToken);

            // Assert
            await _mqttClient.Received().PublishAsync(Arg.Any<MqttApplicationMessage>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_publish_value_types_that_cant_be_serialized()
        {
            // Arrange
            await using var handler = CreateHandler();

            // Act
            await handler.PublishValue(_inputEvent.EventId, "Test", "Some.Invalid.Type", TestContext.Current.CancellationToken);

            // Assert
            await _mqttClient.DidNotReceive().PublishAsync(Arg.Any<MqttApplicationMessage>(), Arg.Any<CancellationToken>());
        }
    }

    public class ResetAndReconnect : DataflowEventMqttHandlerTests
    {
        // Every completed deployment resets the connection; two deployments completing together stop the reconnect
        // loop right after it started.
        [Fact]
        public async Task Should_survive_resets_in_quick_succession()
        {
            // Arrange
            await using var handler = CreateHandler();
            _mqttClientProvider.CreateSystemMqttClientOptions().Returns(new MqttClientOptions());
            await handler.Subscribe([_outputEvent], TestContext.Current.CancellationToken);

            // Act
            for (var i = 0; i < 2000; i++)
                await handler.ResetAndReconnect(TestContext.Current.CancellationToken);

            await handler.Disconnect();

            // Assert
            await _mqttClient.Received(2001).ConnectAsync(Arg.Any<MqttClientOptions>(), Arg.Any<CancellationToken>());
        }
    }

    public class HandleApplicationMessageReceived : DataflowEventMqttHandlerTests
    {
        [Fact]
        public async Task Should_not_handle_topics_that_are_no_valid_guid()
        {
            // Arrange
            await using var handler = CreateHandler();
            var receivedArgs = CreateMessageReceivedArgs("topic");
            var receivedEvent = false;

            handler.EventReceivedAsync += (args) => { receivedEvent = true; return Task.CompletedTask; };
            await handler.Subscribe([_outputEvent], TestContext.Current.CancellationToken);

            // Act
            _mqttClient.ApplicationMessageReceivedAsync += Raise.Event<Func<MqttApplicationMessageReceivedEventArgs, Task>>(receivedArgs);

            // Act + Assert
            receivedEvent.Should().Be(false);
        }

        [Fact]
        public async Task Should_not_invoke_received_if_no_user_properties_are_set()
        {
            // Arrange
            await using var handler = CreateHandler();
            var receivedArgs = CreateMessageReceivedArgs(Guid.NewGuid().ToString());
            var receivedEvent = false;

            handler.EventReceivedAsync += (args) => { receivedEvent = true; return Task.CompletedTask; };
            await handler.Subscribe([_outputEvent], TestContext.Current.CancellationToken);

            // Act
            _mqttClient.ApplicationMessageReceivedAsync += Raise.Event<Func<MqttApplicationMessageReceivedEventArgs, Task>>(receivedArgs);

            // Assert
            receivedEvent.Should().Be(false);
        }

        [Fact]
        public async Task Should_not_handle_topic_that_is_a_valid_guid()
        {
            // Arrange
            await using var handler = CreateHandler();
            var receivedArgs = CreateMessageReceivedArgs(Guid.NewGuid().ToString(), builder =>
            {
                builder.WithUserProperty(MqttUserProperties.Timestamp, DateTime.UtcNow);
            });
            var receivedEvent = false;

            handler.EventReceivedAsync += (args) => { receivedEvent = true; return Task.CompletedTask; };
            await handler.Subscribe([_outputEvent], TestContext.Current.CancellationToken);

            // Act
            _mqttClient.ApplicationMessageReceivedAsync += Raise.Event<Func<MqttApplicationMessageReceivedEventArgs, Task>>(receivedArgs);

            // Assert
            receivedEvent.Should().Be(true);
        }

        private static MqttApplicationMessageReceivedEventArgs CreateMessageReceivedArgs(string topic, Action<MqttApplicationMessageBuilder>? builder = null)
        {
            var messageBuilder = new MqttApplicationMessageBuilder();
            messageBuilder.WithPayload(" ");
            if (!string.IsNullOrEmpty(topic))
                messageBuilder.WithTopic(topic);

            builder?.Invoke(messageBuilder);

            var packet = new MqttPublishPacket();
            return new MqttApplicationMessageReceivedEventArgs("clientID", messageBuilder.Build(), packet, (msg, token) => { return Task.CompletedTask; });
        }
    }
}
