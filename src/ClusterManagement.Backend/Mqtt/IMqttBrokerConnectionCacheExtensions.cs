using MQTTnet;

namespace ClusterManagement.Backend.Mqtt;

internal static class IMqttBrokerConnectionCacheExtensions
{
    internal static async Task<IMqttClient> ConnectClient(this IMqttBrokerConnectionCache connectionCache, IMqttClient? existingSubstitute,
        Func<MqttApplicationMessageReceivedEventArgs, Task>? receiveHandler, CancellationToken cancellationToken)
    {
        var mqttClient = existingSubstitute;
        if (mqttClient is null)
        {
            var options = await connectionCache.GetClientOptionsBuilder(cancellationToken).ConfigureAwait(false);
            mqttClient = new MqttClientFactory().CreateMqttClient();
            await mqttClient.ConnectAsync(options.Build(), cancellationToken).ConfigureAwait(false);
        }
        if (receiveHandler is not null)
            mqttClient.ApplicationMessageReceivedAsync += receiveHandler;
        return mqttClient;
    }

    internal static async Task DisconnectClient(this IMqttClient mqttClient, IMqttClient? existingSubstitute,
        Func<MqttApplicationMessageReceivedEventArgs, Task>? receiveHandler, CancellationToken cancellationToken)
    {
        if (receiveHandler is not null)
            mqttClient.ApplicationMessageReceivedAsync -= receiveHandler;
        if (mqttClient != existingSubstitute)
        {
            await mqttClient.DisconnectAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            mqttClient.Dispose();
        }
    }
}
