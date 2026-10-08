using System.IO.Abstractions;
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
        var fileSystem = services.GetRequiredService<IFileSystem>();
        var connection = await GetMqttConnection(cancellationToken);
        return new MqttClientOptionsBuilder()
            .WithSuiteConnection(connection)
            .WithSslProtocol(connection, fileSystem);
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
