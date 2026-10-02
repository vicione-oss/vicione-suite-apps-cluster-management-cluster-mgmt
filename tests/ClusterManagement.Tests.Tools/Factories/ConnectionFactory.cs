using ClusterManagement.Public.Connections.Contracts;
using ClusterManagement.Public.Connections.Extensions;
using Sdk.Connections.Contracts;

namespace ClusterManagement.Tests.Tools.Factories;

/// <summary>
/// Creates OPC UA test connections.
/// </summary>
/// <remarks>
/// OPC UA connection support is owned by cluster-mgmt and is not part of the published <c>ViciOne.Suite.Sdk.Testing</c>
/// package, so this factory lives here rather than in <c>Sdk.Testing.Factories.ConnectionFactory</c>.
/// </remarks>
public static class ConnectionFactory
{
    /// <summary>
    /// Creates a new <see cref="Connection"/> object configured for a client-side connection to an OPC UA server.
    /// </summary>
    public static Connection CreateOpcUaClientConnection(string name, string server = "localhost", int port = 4840, string endpoint = "ua/dataport", string? rootNodeId = null, Tag[]? tags = null)
    {
        var connection = new Connection
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = ConnectionType.OpcUaClient,
        };

        if (tags is not null)
        {
            foreach (var tag in tags)
                connection.Tags.Add(tag);
        }

        connection.SetOpcUaClientConnection(new OpcUaClientConnection
        {
            Server = server,
            Port = port,
            Endpoint = endpoint,
            RootNodeId = rootNodeId,
        });
        return connection;
    }

    /// <summary>
    /// Creates a new <see cref="Connection"/> object configured for a server-side OPC UA connection.
    /// </summary>
    public static Connection CreateOpcUaServerConnection(string name, string networkInterface = "lan1", string endpoint = "ua/dataport", Tag[]? tags = null)
    {
        var connection = new Connection
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = ConnectionType.OpcUaServer,
        };

        if (tags is not null)
        {
            foreach (var tag in tags)
                connection.Tags.Add(tag);
        }

        connection.SetOpcUaServerConnection(new OpcUaServerConnection
        {
            NetworkInterface = networkInterface,
            Endpoint = endpoint,
        });
        return connection;
    }
}
