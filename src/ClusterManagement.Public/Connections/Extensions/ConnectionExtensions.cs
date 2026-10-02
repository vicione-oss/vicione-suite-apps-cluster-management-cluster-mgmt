using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ClusterManagement.Public.Connections.Contracts;
using Sdk.Connections.Contracts;
using Sdk.Messaging;

namespace ClusterManagement.Public.Connections.Extensions;

/// <summary>
/// Provides OPC UA extension members for <see cref="Sdk.Connections.Contracts.ConnectionType"/> and
/// <see cref="Sdk.Connections.Contracts.Connection"/>.
/// </summary>
/// <remarks>
/// OPC UA connection support is owned by cluster-mgmt and is not part of the published <c>ViciOne.Suite.Sdk</c>
/// package, so <c>ConnectionType.OpcUaClient</c>, <c>ConnectionType.OpcUaServer</c>, and the matching
/// <c>Connection.Set*</c>/<c>Connection.Get*</c> members are defined here as extension members instead of
/// on the SDK types directly.
/// </remarks>
[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "False positive: CA1034 reports the compiler-generated types behind C# 14 extension blocks.")]
public static class ConnectionExtensions
{
    extension(ConnectionType)
    {
        /// <summary>
        /// Gets a connection type representing a client-side OPC UA connection to a server.
        /// </summary>
        public static ConnectionType OpcUaClient => new("opcua-client");

        /// <summary>
        /// Gets a connection type representing a server-side OPC UA connection, i.e. a server endpoint clients connect to.
        /// </summary>
        public static ConnectionType OpcUaServer => new("opcua-server");
    }

    extension(Connection connection)
    {
        /// <summary>
        /// Sets the connection's data to an <see cref="OpcUaClientConnection"/> by serializing it to JSON and updating the type.
        /// </summary>
        public void SetOpcUaClientConnection(OpcUaClientConnection opcUaClientConnection)
        {
            connection.Json = JsonSerializer.Serialize(opcUaClientConnection, DefaultJsonSerializerSettings.Default);
            connection.Type = ConnectionType.OpcUaClient;
        }

        /// <summary>
        /// Gets the strongly-typed <see cref="OpcUaClientConnection"/> details if the connection type is <see cref="extension(ConnectionType).OpcUaClient"/>.
        /// </summary>
        public OpcUaClientConnection? GetOpcUaClientConnection()
            => Deserialize<OpcUaClientConnection>(connection, ConnectionType.OpcUaClient);

        /// <summary>
        /// Sets the connection's data to an <see cref="OpcUaServerConnection"/> by serializing it to JSON and updating the type.
        /// </summary>
        public void SetOpcUaServerConnection(OpcUaServerConnection opcUaServerConnection)
        {
            connection.Json = JsonSerializer.Serialize(opcUaServerConnection, DefaultJsonSerializerSettings.Default);
            connection.Type = ConnectionType.OpcUaServer;
        }

        /// <summary>
        /// Gets the strongly-typed <see cref="OpcUaServerConnection"/> details if the connection type is <see cref="extension(ConnectionType).OpcUaServer"/>.
        /// </summary>
        public OpcUaServerConnection? GetOpcUaServerConnection()
            => Deserialize<OpcUaServerConnection>(connection, ConnectionType.OpcUaServer);
    }

    private static T? Deserialize<T>(Connection connection, ConnectionType type)
        where T : class
    {
        if (connection.Type != type || string.IsNullOrWhiteSpace(connection.Json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(connection.Json, DefaultJsonSerializerSettings.Default);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
