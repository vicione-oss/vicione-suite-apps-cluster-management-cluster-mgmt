using System.ComponentModel;
using MQTTnet;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;

namespace ClusterManagement.Internal.Extensions;

/// <summary>
/// Copy of suite internal extension methods for <see cref="MqttClientOptionsBuilder"/> to support MQTT connections in the cluster management backend.
/// </summary>
public static class MqttClientOptionsBuilderExtensions
{
    public static MqttClientOptionsBuilder WithSuiteConnection(this MqttClientOptionsBuilder builder, MqttConnection connection)
    {
        if (!string.IsNullOrWhiteSpace(connection.Username))
        {
            if (string.IsNullOrEmpty(connection.Password))
                builder.WithCredentials(connection.Username);
            else
                builder.WithCredentials(connection.Username, connection.Password);
        }

        if (!string.IsNullOrWhiteSpace(connection.ClientId))
            builder.WithClientId(connection.ClientId);

        builder
            .WithCleanSession(connection.CleanSession)
            .WithProtocolAndVersion(connection)
            .WithWillOptions(connection);

        return builder;
    }

    private static MqttClientOptionsBuilder WithProtocolAndVersion(this MqttClientOptionsBuilder builder, MqttConnection connection)
    {
        switch (connection.Protocol)
        {
            case MqttConnectionType.TCP:
                builder.WithTcpServer(connection.Address, connection.Port);
                break;
            case MqttConnectionType.WebSocket:
                builder.WithWebSocketServer(b => b.WithUri(connection.GetWebsocketUri().ToString()));
                break;
            default:
                throw new InvalidEnumArgumentException(nameof(connection.Protocol),
                    (int)connection.Protocol,
                    typeof(MqttConnectionType));
        }

        builder.WithProtocolVersion(ToMqttProtocolVersion(connection.ProtocolVersion));

        return builder;

        static MQTTnet.Formatter.MqttProtocolVersion ToMqttProtocolVersion(MqttProtocolVersion proctocolVersion) => proctocolVersion switch
        {
            MqttProtocolVersion.V311 => MQTTnet.Formatter.MqttProtocolVersion.V311,
            MqttProtocolVersion.V500 => MQTTnet.Formatter.MqttProtocolVersion.V500,
            _ => throw new InvalidEnumArgumentException(nameof(proctocolVersion), (int)proctocolVersion, typeof(MqttProtocolVersion)),
        };
    }

    private static MqttClientOptionsBuilder WithWillOptions(this MqttClientOptionsBuilder builder, MqttConnection connection)
    {
        if (string.IsNullOrWhiteSpace(connection.WillMessage) || string.IsNullOrWhiteSpace(connection.WillTopic))
            return builder;

        builder.WithWillPayload(connection.WillMessage);
        builder.WithWillTopic(connection.WillTopic);
        builder.WithWillRetain(connection.WillRetain);
        builder.WithWillQualityOfServiceLevel(ToMqttQualityOfServiceLevel(connection.QualityOfService));

        return builder;

        static MQTTnet.Protocol.MqttQualityOfServiceLevel ToMqttQualityOfServiceLevel(MqttQualityOfServiceLevel serviceLevel) => serviceLevel switch
        {
            MqttQualityOfServiceLevel.AtMostOnce => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce,
            MqttQualityOfServiceLevel.AtLeastOnce => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce,
            MqttQualityOfServiceLevel.ExactlyOnce => MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce,
            _ => throw new InvalidEnumArgumentException(nameof(serviceLevel), (int)serviceLevel, typeof(MqttQualityOfServiceLevel)),
        };
    }
}
