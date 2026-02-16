using ClusterManagement.Client.DataflowEvents.Services;
using Sdk.Client.Infrastructure;
using Sdk.Connections.Requests;
using Sdk.Testing.Client;
using Sdk.Testing.Factories;

namespace ClusterManagement.Client.Tests.DataflowEvents.Services;

public class DataflowEventMqttClientProviderTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();

    private DataflowEventMqttClientProvider CreateProvider() => new(_mediator);

    public class CreateSystemMqttClientOptions : DataflowEventMqttClientProviderTests
    {
        [Fact]
        public async Task Should_throw_if_system_connection_cant_be_loaded()
        {
            // Arrange
            var provider = CreateProvider();
            var action = () => provider.CreateSystemMqttClientOptions();

            _mediator.SetupGetConnections([]);

            // Act + Assert
            await action.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_load_system_mqtt_connection()
        {
            // Arrange
            var provider = CreateProvider();
            var tcpConnection = ConnectionFactory.CreateMqttServiceConnection("mqtt");
            var webSocketConnection = ConnectionFactory.CreateMqttServiceConnection("wsmqtt", protocol: Sdk.Connections.Contracts.MqttConnectionType.WebSocket);

            _mediator.SetupGetConnections([tcpConnection, webSocketConnection]);

            // Act
            var options = await provider.CreateSystemMqttClientOptions();

            // Assert
            options.Should().NotBeNull();
            await _mediator.Received().Request<GetConnections, GetConnectionsResponse>(Arg.Any<GetConnections>(), Arg.Any<CancellationToken>());
        }
    }
}
