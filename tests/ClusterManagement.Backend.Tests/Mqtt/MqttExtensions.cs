using MQTTnet;
using NSubstitute.Core;

namespace ClusterManagement.Backend.Tests.Mqtt;

internal static class MqttExtensions
{
    internal static void ReceiveApplicationMessage(this IMqttClient mqttClient, Action<MqttApplicationMessageBuilder> receive)
    {
        MqttApplicationMessageBuilder receiveBuilder = new();
        receive(receiveBuilder);
        MqttApplicationMessageReceivedEventArgs eventArgs = new(Guid.NewGuid().ToString(), receiveBuilder.Build(), new(), (_, _) => Task.CompletedTask);
        mqttClient.ApplicationMessageReceivedAsync += Raise.Event<Func<MqttApplicationMessageReceivedEventArgs, Task>>(eventArgs);
    }

    internal static void ReceiveApplicationMessage(this IMqttClient mqttClient, CallInfo publishCall,
        Action<MqttApplicationMessage, MqttApplicationMessageBuilder> receive)
    {
        MqttApplicationMessageBuilder receiveBuilder = new();
        var publish = publishCall.Arg<MqttApplicationMessage>();
        receive(publish, receiveBuilder);
        MqttApplicationMessageReceivedEventArgs eventArgs = new(Guid.NewGuid().ToString(), receiveBuilder.Build(), new(), (_, _) => Task.CompletedTask);
        mqttClient.ApplicationMessageReceivedAsync += Raise.Event<Func<MqttApplicationMessageReceivedEventArgs, Task>>(eventArgs);
    }

    internal static string? GetTopic(this MqttClientSubscribeOptions options) => options.TopicFilters.FirstOrDefault()?.Topic;
}
