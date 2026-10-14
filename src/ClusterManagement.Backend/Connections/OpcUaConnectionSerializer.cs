using ClusterManagement.Public.Connections.Contracts;
using Sdk.Connections;
using Sdk.Connections.Contracts;

namespace ClusterManagement.Backend.Connections;

/// <summary>
/// Serializes OPC UA connections like the default serializer, and converts the store paths 3.0.0-beta2 created to relative
/// ones when it reads a connection.
/// </summary>
/// <remarks>
/// The settings dialog reads a connection with it and writes it back only when the connection is saved, so a connection
/// is migrated when it is changed anyway, without an update of its own.
/// </remarks>
internal sealed class OpcUaConnectionSerializer<TConnection> : IConnectionSerializer
    where TConnection : class, IConnection
{
    private readonly DefaultJsonConnectionSerializer<TConnection> _serializer = new();

    public string Serialize(IConnection connection) => _serializer.Serialize(connection);

    public IConnection? Deserialize(string json)
        => _serializer.Deserialize(json) switch
        {
            OpcUaClientConnection client => client with
            {
                ApplicationCertificatesPath = OpcUaStorePaths.NormalizeStorePath(client.ApplicationCertificatesPath),
                TrustedCertificatesPath = OpcUaStorePaths.NormalizeStorePath(client.TrustedCertificatesPath),
                TrustedIssuerCertificatesPath = OpcUaStorePaths.NormalizeStorePath(client.TrustedIssuerCertificatesPath),
            },
            OpcUaServerConnection server => server with
            {
                ApplicationCertificatesPath = OpcUaStorePaths.NormalizeStorePath(server.ApplicationCertificatesPath),
                TrustedCertificatesPath = OpcUaStorePaths.NormalizeStorePath(server.TrustedCertificatesPath),
                TrustedIssuerCertificatesPath = OpcUaStorePaths.NormalizeStorePath(server.TrustedIssuerCertificatesPath),
            },
            var other => other,
        };
}
