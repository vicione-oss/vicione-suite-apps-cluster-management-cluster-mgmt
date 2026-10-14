namespace ClusterManagement.Public.Connections;

/// <summary>
/// Ids and values of the OPC-UA Client and Server DataPorts an OPC UA connection maps onto.
/// </summary>
/// <remarks>
/// These have to match the DataPorts' rulesets (<c>OpcUaClient.yaml</c>, <c>OpcUaServer.yaml</c> in dp-opcua).
/// </remarks>
internal static class OpcUaDataPort
{
    internal const string ClientDesignId = "OpcUaClientInstance";
    internal const string ServerDesignId = "OpcUaServerInstance";

    internal const string ApplicationCertificatesStoreTypePropertyId = "ApplicationCertificatesStoreType";
    internal const string ApplicationCertificatesStorePathPropertyId = "ApplicationCertificatesStorePath";
    internal const string TrustedCertificatesStoreTypePropertyId = "TrustedCertificatesStoreType";
    internal const string TrustedCertificatesStorePathPropertyId = "TrustedCertificatesStorePath";
    internal const string TrustedIssuerCertificatesStoreTypePropertyId = "TrustedIssuerCertificatesStoreType";
    internal const string TrustedIssuerCertificatesStorePathPropertyId = "TrustedIssuerCertificatesStorePath";

    // the store type properties are UInt8 lists: 0 None, 1 X509 Store, 2 Directory
    internal const byte StoreTypeNone = 0;
    internal const byte StoreTypeDirectory = 2;
}
