using ClusterManagement.Public.Connections.Contracts;

namespace ClusterManagement.Backend.Connections;

/// <summary>
/// Creates new OPC UA connections whose certificate stores each get a directory of their own below the workspace home.
/// </summary>
/// <remarks>
/// The directories do not need to exist, the OPC UA stack creates a store directory once it adds a certificate to it.
/// </remarks>
internal sealed class OpcUaConnectionFactory
{
    public OpcUaClientConnection CreateClientConnection()
    {
        var (application, trusted, trustedIssuer) = OpcUaStorePaths.CreateStorePaths();

        return new()
        {
            ApplicationCertificatesPath = application,
            TrustedCertificatesPath = trusted,
            TrustedIssuerCertificatesPath = trustedIssuer,
        };
    }

    public OpcUaServerConnection CreateServerConnection()
    {
        var (application, trusted, trustedIssuer) = OpcUaStorePaths.CreateStorePaths();

        return new()
        {
            ApplicationCertificatesPath = application,
            TrustedCertificatesPath = trusted,
            TrustedIssuerCertificatesPath = trustedIssuer,
        };
    }
}
