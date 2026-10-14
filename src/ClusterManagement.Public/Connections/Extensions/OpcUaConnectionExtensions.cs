using System.Diagnostics.CodeAnalysis;
using ClusterManagement.Public.Connections.Contracts;

namespace ClusterManagement.Public.Connections.Extensions;

/// <summary>
/// Provides extension members that map OPC UA connections onto the properties of the OPC-UA Client and Server DataPorts.
/// </summary>
[SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "False positive: CA1034 reports the compiler-generated types behind C# 14 extension blocks.")]
public static class OpcUaConnectionExtensions
{
    extension(OpcUaClientConnection connection)
    {
        /// <summary>
        /// Gets the OPC-UA Client DataPort's certificate store properties for this connection, keyed by DataPort property id.
        /// </summary>
        /// <remarks>
        /// Contains a store type and a store path property for each of the application, trusted and trusted issuer stores.
        /// A store with a path is a directory store, a store without one has no store. The paths are passed on as the
        /// connection stores them, cluster management resolves them when it deploys the DataPort.
        /// </remarks>
        public IReadOnlyDictionary<string, object?> GetCertificateStoreProperties()
            => CreateCertificateStoreProperties(connection.ApplicationCertificatesPath, connection.TrustedCertificatesPath, connection.TrustedIssuerCertificatesPath);
    }

    extension(OpcUaServerConnection connection)
    {
        /// <summary>
        /// Gets the OPC-UA Server DataPort's certificate store properties for this connection, keyed by DataPort property id.
        /// </summary>
        /// <remarks>
        /// Contains a store type and a store path property for each of the application, trusted and trusted issuer stores.
        /// A store with a path is a directory store, a store without one has no store. The paths are passed on as the
        /// connection stores them, cluster management resolves them when it deploys the DataPort.
        /// </remarks>
        public IReadOnlyDictionary<string, object?> GetCertificateStoreProperties()
            => CreateCertificateStoreProperties(connection.ApplicationCertificatesPath, connection.TrustedCertificatesPath, connection.TrustedIssuerCertificatesPath);
    }

    private static Dictionary<string, object?> CreateCertificateStoreProperties(string? application, string? trusted, string? trustedIssuer)
        => new()
        {
            [OpcUaDataPort.ApplicationCertificatesStoreTypePropertyId] = GetStoreType(application),
            [OpcUaDataPort.ApplicationCertificatesStorePathPropertyId] = application,
            [OpcUaDataPort.TrustedCertificatesStoreTypePropertyId] = GetStoreType(trusted),
            [OpcUaDataPort.TrustedCertificatesStorePathPropertyId] = trusted,
            [OpcUaDataPort.TrustedIssuerCertificatesStoreTypePropertyId] = GetStoreType(trustedIssuer),
            [OpcUaDataPort.TrustedIssuerCertificatesStorePathPropertyId] = trustedIssuer,
        };

    private static byte GetStoreType(string? path)
        => string.IsNullOrEmpty(path) ? OpcUaDataPort.StoreTypeNone : OpcUaDataPort.StoreTypeDirectory;
}
