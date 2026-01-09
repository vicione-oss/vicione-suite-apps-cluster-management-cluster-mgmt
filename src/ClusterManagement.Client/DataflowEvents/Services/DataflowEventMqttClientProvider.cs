using ClusterManagement.Client.Management.Extensions;
using MQTTnet;
using Sdk.Client.Infrastructure;
using Sdk.Connections;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;
using Sdk.Connections.Requests;

namespace ClusterManagement.Client.DataflowEvents.Services;

internal sealed class DataflowEventMqttClientProvider(IUiMediator mediator) : IDataflowEventMqttClientProvider
{
    private readonly IUiMediator _mediator = mediator;
    private MqttConnection? _connection;

    /// <summary>
    /// used to abstract the IMqttClient for testing
    /// </summary>
    /// <returns></returns>
    public IMqttClient CreateMqttClient()
        => new MqttClientFactory().CreateMqttClient();

    public async Task<MqttClientOptions> CreateSystemMqttClientOptions()
    {
        _connection ??= await LoadSystemMqttConnection();
        if (_connection is null)
            throw new InvalidOperationException("Failed to load system mqtt connection");

        return new MqttClientOptionsBuilder()
                .WithSuiteConnection(_connection)
                .WithClientId($"cm-events-{Guid.NewGuid()}")
                .Build();
    }

    private async Task<MqttConnection?> LoadSystemMqttConnection()
    {
        var request = new GetConnections(null,
            [ConnectionType.Mqtt.Name],
            [ConnectionConstants.Tags.SystemDefault]);

        // identify our first connection
        var connections = (await _mediator.Request<GetConnections, GetConnectionsResponse>(request, CancellationToken.None)).Connections;
        if (connections.Count == 0)
            return null;

        // todo - on wasm render mode client should use websocket connection
        return connections.Select(k => k.GetMqttConnection()).FirstOrDefault(k => k?.Protocol != MqttConnectionType.WebSocket);
    }
}
