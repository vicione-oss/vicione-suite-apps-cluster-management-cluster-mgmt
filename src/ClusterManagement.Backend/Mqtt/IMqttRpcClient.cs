namespace ClusterManagement.Backend.Mqtt;

public interface IMqttRpcClient
{
    Task<MqttRpcResponse> Request(string requestTopic, MqttRpcRequest request, string responseTopic, CancellationToken cancellationToken);
}
