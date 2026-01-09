using ClusterManagement.Public;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;
using Sdk.Connections.Requests;

namespace ClusterManagement.Internal.Extensions;

public static class GetConnectionsResponseExtension
{
    public static Connection FindMqttBrokerConnection(this GetConnectionsResponse response, Guid localInstanceId)
        => response.Connections.FirstOrDefault(c => c.Tags.Contains(Constants.CentralBroker))
                ?? response.Connections.FirstOrDefaultInstanceMqttConnection(localInstanceId, MqttConnectionType.TCP)
                ?? throw new InvalidOperationException("No suite MQTT broker configured.");

}
