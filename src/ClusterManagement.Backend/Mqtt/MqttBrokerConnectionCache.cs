using System.ComponentModel;
using ClusterManagement.Internal.Extensions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using Sdk.Backend.Messaging;
using Sdk.Connections;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Connections.Extensions;
using Sdk.Connections.Requests;
using Sdk.Instance;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Mqtt;

[ReadOnlyConsumer]
public sealed class MqttBrokerConnectionCache(IServiceProvider services) : IMqttBrokerConnectionCache, IConsumer<ConnectionChanged>
{
    private MqttConnection? _connection;
    private Guid? _connectionId;
    private readonly Lock _lockObject = new();

    public async Task<MqttClientOptionsBuilder> GetClientOptionsBuilder(CancellationToken cancellationToken)
    {
        var conn = await GetMqttConnection(cancellationToken);
        var builder = new MqttClientOptionsBuilder()
            .WithCredentials(conn.Username, conn.Password)
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500);

        return conn.Protocol switch
        {
            MqttConnectionType.WebSocket => builder.WithWebSocketServer(b => b.WithUri(conn.Address)),
            MqttConnectionType.TCP => builder.WithTcpServer(conn.Address, conn.Port),
            MqttConnectionType.TCPWithTLS => builder.WithTcpServer(conn.Address, conn.Port).WithTlsOptions(tls => tls.UseTls()),
            _ => throw new InvalidEnumArgumentException(nameof(MqttConnection.Protocol), (int)conn.Protocol, typeof(MqttConnectionType))
        };
    }

    public async Task<MqttConnection> GetMqttConnection(CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            var conn = await LoadBrokerConnection(cancellationToken);
            var mqttConn = conn.GetMqttConnection() ?? throw new InvalidOperationException("No suite MQTT broker configured.");

            lock (_lockObject)
            {
                _connection = mqttConn;
                _connectionId = conn.Id;
                return _connection with { };
            }
        }

        lock (_lockObject)
            return _connection with { };
    }

    private async Task<Connection> LoadBrokerConnection(CancellationToken cancellationToken)
    {
        var request = new GetConnections(null,
            [ConnectionType.Mqtt.Name],
            [ConnectionConstants.Tags.SystemDefault]);

        using var scope = services.CreateScope();
        var infoProvider = scope.ServiceProvider.GetRequiredService<IInstanceInformationProvider>();
        var mediator = scope.ServiceProvider.GetRequiredService<ISuiteMediator>();

        var response = await mediator.Request<GetConnections, GetConnectionsResponse>(request, cancellationToken);

        return response.FindMqttBrokerConnection(infoProvider.Local.Id);
    }

    public Task Consume(ConsumeContext<ConnectionChanged> context)
    {
        if (context.Message.Connection.Id == _connectionId)
        {
            lock (_lockObject)
                _connection = null;
        }

        return Task.CompletedTask;
    }
}
