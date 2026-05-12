using MQTTnet;

namespace ClusterManagement.Client.DataflowEvents.Services;

internal interface IDataflowEventMqttClientProvider
{
    IMqttClient CreateMqttClient();

    Task<MqttClientOptions> CreateSystemMqttClientOptions();

    void ResetConnection();
}
