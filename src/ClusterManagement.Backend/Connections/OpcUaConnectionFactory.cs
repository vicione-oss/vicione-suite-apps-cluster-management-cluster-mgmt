using System.IO.Abstractions;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Public.Connections.Contracts;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Connections;

/// <summary>
/// Creates new OPC UA connections whose certificate stores each get a directory of their own below the workspace home.
/// </summary>
/// <remarks>
/// The directories do not need to exist, the OPC UA stack creates a store directory once it adds a certificate to it.
/// </remarks>
internal sealed class OpcUaConnectionFactory(IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider)
{
    public OpcUaClientConnection CreateClientConnection()
    {
        var (application, trusted, trustedIssuer) = CreateCertificatesPaths();

        return new()
        {
            ApplicationCertificatesPath = application,
            TrustedCertificatesPath = trusted,
            TrustedIssuerCertificatesPath = trustedIssuer,
        };
    }

    public OpcUaServerConnection CreateServerConnection()
    {
        var (application, trusted, trustedIssuer) = CreateCertificatesPaths();

        return new()
        {
            ApplicationCertificatesPath = application,
            TrustedCertificatesPath = trusted,
            TrustedIssuerCertificatesPath = trustedIssuer,
        };
    }

    private (string Application, string Trusted, string TrustedIssuer) CreateCertificatesPaths()
    {
        // every connection gets a directory of its own, so connections never share or overwrite each other's certificates
        var directory = fileSystem.Path.Combine(fileSystem.GetOpcUaCertificatesPath(workspaceProvider), Guid.NewGuid().ToString("N"));

        return (
            fileSystem.Path.Combine(directory, "application"),
            fileSystem.Path.Combine(directory, "trusted"),
            fileSystem.Path.Combine(directory, "trusted-issuer"));
    }
}
