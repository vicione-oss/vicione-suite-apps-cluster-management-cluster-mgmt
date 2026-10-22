using System.IO.Abstractions;
using ClusterManagement.Internal.Extensions;
using MQTTnet;
using Sdk.Client.Infrastructure;
using Sdk.Connections;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;
using Sdk.Connections.Requests;

namespace ClusterManagement.Client.Management.Services;

internal sealed class EngineHostBrokerProvider(IUiMediator mediator, IFileSystem fileSystem) : IEngineHostBrokerProvider
{
    public async Task<IReadOnlyDictionary<Guid, EngineHostBroker>> FindBrokers(IReadOnlyCollection<Guid> instanceIds, CancellationToken cancellationToken)
    {
        // The same request the deployment uses to choose the engine hosts' broker.
        var request = new GetConnections(null, [ConnectionType.Mqtt.Name], [ConnectionConstants.Tags.SystemDefault]);
        var response = await mediator.Request<GetConnections, GetConnectionsResponse>(request, cancellationToken);

        var brokers = new Dictionary<Guid, EngineHostBroker>();
        foreach (var instanceId in instanceIds)
        {
            if (response.FindMqttBrokerConnectionOrDefault(instanceId) is { } connection && connection.GetMqttConnection() is { } mqtt)
                brokers[instanceId] = new EngineHostBroker(connection.Id, mqtt, ToOptions(mqtt));
        }

        return brokers;
    }

    // The same TLS settings the backend connects the engine hosts with.
    private MqttClientOptions ToOptions(MqttConnection mqtt)
        => new MqttClientOptionsBuilder()
            .WithSuiteConnection(mqtt, fileSystem)
            .WithClientId($"cm-cycles-{Guid.NewGuid()}")
            .Build();
}
