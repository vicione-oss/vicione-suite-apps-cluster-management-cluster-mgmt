using System.IO.Abstractions.TestingHelpers;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Public;
using MQTTnet;
using Sdk.Client.Infrastructure;
using Sdk.Connections;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;
using Sdk.Connections.Requests;

namespace ClusterManagement.Client.Tests.Management;

public class EngineHostBrokerProviderTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly Guid _master = Guid.NewGuid();
    private readonly Guid _slave = Guid.NewGuid();

    [Fact]
    public async Task Each_instance_gets_its_own_broker_without_a_central_one()
    {
        // Arrange
        var masterBroker = InstanceBroker(_master, "master-host");
        var slaveBroker = InstanceBroker(_slave, "slave-host");
        ConnectionsAre(masterBroker, slaveBroker);
        var sut = new EngineHostBrokerProvider(_mediator, _fileSystem);

        // Act
        var brokers = await sut.FindBrokers([_master, _slave], TestContext.Current.CancellationToken);

        // Assert
        brokers[_master].ConnectionId.Should().Be(masterBroker.Id);
        brokers[_slave].ConnectionId.Should().Be(slaveBroker.Id);
    }

    [Fact]
    public async Task A_broker_carries_the_settings_of_its_connection()
    {
        // Arrange
        ConnectionsAre(InstanceBroker(_master, "master-host"));
        var sut = new EngineHostBrokerProvider(_mediator, _fileSystem);

        // Act
        var brokers = await sut.FindBrokers([_master], TestContext.Current.CancellationToken);

        // Assert
        brokers[_master].Connection.Address.Should().Be("master-host");
    }

    [Fact]
    public async Task Every_instance_uses_the_central_broker_when_one_is_configured()
    {
        // Arrange
        var central = Broker("central-host");
        central.Tags.Add(Constants.CentralBroker);
        ConnectionsAre(InstanceBroker(_master, "master-host"), InstanceBroker(_slave, "slave-host"), central);
        var sut = new EngineHostBrokerProvider(_mediator, _fileSystem);

        // Act
        var brokers = await sut.FindBrokers([_master, _slave], TestContext.Current.CancellationToken);

        // Assert
        brokers.Values.Select(b => b.ConnectionId).Should().AllBeEquivalentTo(central.Id);
    }

    [Fact]
    public async Task An_instance_without_a_broker_is_left_out()
    {
        // Arrange
        ConnectionsAre(InstanceBroker(_master, "master-host"));
        var sut = new EngineHostBrokerProvider(_mediator, _fileSystem);

        // Act
        var brokers = await sut.FindBrokers([_master, _slave], TestContext.Current.CancellationToken);

        // Assert
        brokers.Keys.Should().Equal(_master);
    }

    [Theory]
    [InlineData(MqttSslProtocol.Tls12, true)]
    [InlineData(null, false)]
    public async Task A_broker_is_connected_with_the_tls_setting_of_its_connection(MqttSslProtocol? sslProtocol, bool usesTls)
    {
        // Arrange
        var broker = InstanceBroker(_master, "master-host");
        broker.SetMqttConnection(broker.GetMqttConnection()! with { SslProtocol = sslProtocol, Port = 8883 });
        ConnectionsAre(broker);
        var sut = new EngineHostBrokerProvider(_mediator, _fileSystem);

        // Act
        var brokers = await sut.FindBrokers([_master], TestContext.Current.CancellationToken);

        // Assert
        brokers[_master].Options.ChannelOptions.Should().BeOfType<MqttClientTcpOptions>()
            .Which.TlsOptions.UseTls.Should().Be(usesTls);
    }

    private void ConnectionsAre(params Connection[] connections)
        => _mediator.Request<GetConnections, GetConnectionsResponse>(Arg.Any<GetConnections>(), Arg.Any<CancellationToken>())
            .Returns(new GetConnectionsResponse([.. connections]));

    private static Connection InstanceBroker(Guid instanceId, string address)
    {
        var connection = Broker(address);
        connection.Metadata[ConnectionConstants.MetaDataKeys.InstanceId] = instanceId.ToString();
        connection.Metadata[ConnectionConstants.MetaDataKeys.MqttClientProtocol] = nameof(MqttConnectionType.TCP);
        return connection;
    }

    private static Connection Broker(string address)
    {
        var connection = new Connection { Id = Guid.NewGuid() };
        connection.SetMqttConnection(new MqttConnection { Address = address, Port = 1883, Protocol = MqttConnectionType.TCP });
        return connection;
    }
}
