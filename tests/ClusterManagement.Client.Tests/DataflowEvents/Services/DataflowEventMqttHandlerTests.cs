using AwesomeAssertions;
using ClusterManagement.Client.DataflowEvents.Services;
using ClusterManagement.Client.Management.Extensions;
using ClusterManagement.Public.DataflowEvents;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Packets;
using NSubstitute;
using Xunit;
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
            _mqttClient.ConnectAsync(Arg.Any<MqttClientOptions>()).Returns(Task.FromResult(new MqttClientConnectResult()));

        return new DataflowEventMqttHandler(_mqttClientProvider, _logger);
    }

    public class Subscribe : DataflowEventMqttHandlerTests
    {
        [Fact]
        public async Task Should_throw_if_connection_fails()
        {
            // Arrange
            await using var handler = CreateHandler(false);
            var action = () => handler.Subscribe(_outputEvent);

            // Act + Assert
            await action.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_ensure_connection_is_established()
        {
            // Arrange
            await using var handler = CreateHandler();

            // Act
            await handler.Subscribe(_outputEvent);

            // Assert
            await _mqttClient.Received().ConnectAsync(Arg.Any<MqttClientOptions>());
        }

        [Fact]
        public async Task Should_not_connect_if_already_connected()
        {
            // Arrange
            await using var handler = CreateHandler();
            _mqttClient.IsConnected.Returns(true);

            // Act
            await handler.Subscribe(_outputEvent);

            // Assert
            await _mqttClient.DidNotReceive().ConnectAsync(Arg.Any<MqttClientOptions>());
        }

        [Fact]
        public async Task Should_subscribe_to_mqtt_topic()
        {
            // Arrange
            await using var handler = CreateHandler();

            // Act
            await handler.Subscribe(_outputEvent);

            // Assert            
            await _mqttClient.Received().SubscribeAsync(Arg.Any<MqttClientSubscribeOptions>());
        }
    }

    public class Unsubscribe : DataflowEventMqttHandlerTests
    {
        [Fact]
        public async Task Should_throw_if_connection_fails()
        {
            // Arrange
            await using var handler = CreateHandler(false);
            var action = () => handler.Subscribe(_outputEvent);

            // Act + Assert
            await action.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_ensure_connection_is_established()
        {
            // Arrange
            await using var handler = CreateHandler();

            // Act
            await handler.Subscribe(_outputEvent);

            // Assert
            await _mqttClient.Received().ConnectAsync(Arg.Any<MqttClientOptions>());
        }

        [Fact]
        public async Task Should_unsubscribe_from_mqtt_topic()
        {
            // Arrange
            await using var handler = CreateHandler();

            // Act
            await handler.Unsubscribe(_outputEvent);

            // Assert            
            await _mqttClient.Received().UnsubscribeAsync(Arg.Any<MqttClientUnsubscribeOptions>());
        }
    }

    public class PublishValue : DataflowEventMqttHandlerTests
    {
        [Fact]
        public async Task Should_throw_if_connection_cant_be_established()
        {
            // Arrange
            await using var handler = CreateHandler(false);
            var action = () => handler.Subscribe(_outputEvent);

            // Act + Assert
            await action.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_ensure_connection_is_established()
        {
            // Arrange
            await using var handler = CreateHandler();

            // Act
            await handler.Subscribe(_outputEvent);

            // Assert
            await _mqttClient.Received().ConnectAsync(Arg.Any<MqttClientOptions>());
        }

        [Fact]
        public async Task Should_publish_value_to_mqtt_broker()
        {
            // Arrange
            await using var handler = CreateHandler();

            // Act
            await handler.PublishValue(_inputEvent.EventId, "Test", typeof(string).AssemblyQualifiedName!);

            // Assert
            await _mqttClient.Received().PublishAsync(Arg.Any<MqttApplicationMessage>(), default);
        }

        [Fact]
        public async Task Should_not_publish_value_types_that_cant_be_serialized()
        {
            // Arrange
            await using var handler = CreateHandler();

            // Act
            await handler.PublishValue(_inputEvent.EventId, "Test", "Some.Invalid.Type");

            // Assert
            await _mqttClient.DidNotReceive().PublishAsync(Arg.Any<MqttApplicationMessage>(), default);
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
            await handler.Subscribe(_outputEvent);

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
            await handler.Subscribe(_outputEvent);

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
            await handler.Subscribe(_outputEvent);

            // Act
            _mqttClient.ApplicationMessageReceivedAsync += Raise.Event<Func<MqttApplicationMessageReceivedEventArgs, Task>>(receivedArgs);

            // Assert
            receivedEvent.Should().Be(true);
        }

        private static MqttApplicationMessageReceivedEventArgs CreateMessageReceivedArgs(string topic, Action<MqttApplicationMessageBuilder>? builder = null)
        {
            var messageBuilder = new MqttApplicationMessageBuilder();
            if (!string.IsNullOrEmpty(topic))
                messageBuilder.WithTopic(topic);

            builder?.Invoke(messageBuilder);

            var packet = new MqttPublishPacket();
            return new MqttApplicationMessageReceivedEventArgs("clientID", messageBuilder.Build(), packet, (msg, token) => { return Task.CompletedTask; });
        }
    }
}
