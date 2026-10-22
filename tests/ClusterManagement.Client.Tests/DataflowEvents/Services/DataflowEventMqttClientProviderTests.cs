using System.IO.Abstractions.TestingHelpers;
using ClusterManagement.Client.DataflowEvents.Services;
using Sdk.Client.Infrastructure;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;
using Sdk.Connections.Requests;
using Sdk.Testing.Client;
using Sdk.Testing.Factories;

namespace ClusterManagement.Client.Tests.DataflowEvents.Services;

public class DataflowEventMqttClientProviderTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly MockFileSystem _fileSystem = new();

    private DataflowEventMqttClientProvider CreateProvider() => new(_mediator, _fileSystem);

    public class CreateSystemMqttClientOptions : DataflowEventMqttClientProviderTests
    {
        [Fact]
        public async Task Should_throw_if_system_connection_cant_be_loaded()
        {
            // Arrange
            using var provider = CreateProvider();
            var action = () => provider.CreateSystemMqttClientOptions();

            _mediator.SetupGetConnections([]);

            // Act + Assert
            await action.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Should_load_system_mqtt_connection()
        {
            // Arrange
            using var provider = CreateProvider();
            var tcpConnection = ConnectionFactory.CreateMqttServiceConnection("mqtt");
            var webSocketConnection = ConnectionFactory.CreateMqttServiceConnection("wsmqtt", protocol: Sdk.Connections.Contracts.MqttConnectionType.WebSocket);

            _mediator.SetupGetConnections([tcpConnection, webSocketConnection]);

            // Act
            var options = await provider.CreateSystemMqttClientOptions();

            // Assert
            options.Should().NotBeNull();
            await _mediator.Received().Request<GetConnections, GetConnectionsResponse>(Arg.Any<GetConnections>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_apply_the_tls_setting_of_the_system_connection()
        {
            // Arrange
            using var provider = CreateProvider();
            var connection = ConnectionFactory.CreateMqttServiceConnection("mqtt");
            connection.SetMqttConnection(connection.GetMqttConnection()! with { SslProtocol = MqttSslProtocol.Tls12 });

            _mediator.SetupGetConnections([connection]);

            // Act
            var options = await provider.CreateSystemMqttClientOptions();

            // Assert
            options.ChannelOptions.TlsOptions.UseTls.Should().BeTrue();
        }
    }
}
