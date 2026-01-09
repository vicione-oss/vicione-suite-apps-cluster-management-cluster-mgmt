using System.ComponentModel;
using MQTTnet;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;

namespace ClusterManagement.Client.Management.Extensions;

/// <summary>
/// copy of Core.Shared.Extensions.MqttClientOptionsBuilderExtensions
/// </summary>
public static class MqttClientOptionsBuilderExtensions
{
    public static MqttClientOptionsBuilder WithSuiteConnection(this MqttClientOptionsBuilder builder, MqttConnection connection)
    {
        builder.WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500);

        if (!string.IsNullOrEmpty(connection.Username) || !string.IsNullOrEmpty(connection.Password))
            builder.WithCredentials(connection.Username, connection.Password);

        switch (connection.Protocol)
        {
            case MqttConnectionType.TCP:
                builder.WithTcpServer(connection.Address, connection.Port);
                break;
            case MqttConnectionType.TCPWithTLS:
                builder.WithTcpServer(connection.Address, connection.Port)
                    .WithTlsOptions(tls =>
                    {
                        tls.UseTls();
                    });
                break;
            case MqttConnectionType.WebSocket:
                builder.WithWebSocketServer(b => b.WithUri(connection.GetWebsocketUri().ToString()));
                break;
            default:
                throw new InvalidEnumArgumentException(nameof(connection.Protocol), (int)connection.Protocol, typeof(MqttConnectionType));
        }

        return builder;
    }
}
