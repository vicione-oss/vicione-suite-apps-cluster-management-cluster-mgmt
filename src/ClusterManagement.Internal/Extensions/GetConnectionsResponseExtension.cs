using ClusterManagement.Public;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;
using Sdk.Connections.Requests;

namespace ClusterManagement.Internal.Extensions;

public static class GetConnectionsResponseExtension
{
    public static Connection FindMqttBrokerConnection(this GetConnectionsResponse response, Guid localInstanceId)
        => response.FindMqttBrokerConnectionOrDefault(localInstanceId)
                ?? throw new InvalidOperationException("No suite MQTT broker configured.");

    /// <summary>
    /// The broker the engine hosts of <paramref name="instanceId"/> are deployed with: the central broker when one is
    /// configured, otherwise the instance's own TCP broker; <c>null</c> without either.
    /// </summary>
    public static Connection? FindMqttBrokerConnectionOrDefault(this GetConnectionsResponse response, Guid instanceId)
        => response.Connections.FirstOrDefault(c => c.Tags.Contains(Constants.CentralBroker))
                ?? response.Connections.FirstOrDefaultInstanceMqttConnection(instanceId, MqttConnectionType.TCP);
}
