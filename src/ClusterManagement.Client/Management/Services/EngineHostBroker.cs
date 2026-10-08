using MQTTnet;
using Sdk.Connections.Contracts;

namespace ClusterManagement.Client.Management.Services;

/// <summary>
/// A broker engine hosts publish their cycle reports to; <see cref="ConnectionId"/> tells two brokers apart and
/// <see cref="Connection"/> tells whether the settings of one broker changed.
/// </summary>
internal sealed record EngineHostBroker(Guid ConnectionId, MqttConnection Connection, MqttClientOptions Options);
