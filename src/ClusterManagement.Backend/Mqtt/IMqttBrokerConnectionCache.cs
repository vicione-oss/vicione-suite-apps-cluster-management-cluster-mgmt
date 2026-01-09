using MQTTnet;
using Sdk.Connections.Contracts;

namespace ClusterManagement.Backend.Mqtt;

public interface IMqttBrokerConnectionCache
{
    Task<MqttClientOptionsBuilder> GetClientOptionsBuilder(CancellationToken cancellationToken);
    Task<MqttConnection> GetMqttConnection(CancellationToken cancellationToken);
}
