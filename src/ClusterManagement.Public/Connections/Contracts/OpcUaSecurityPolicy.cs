namespace ClusterManagement.Public.Connections.Contracts;

/// <summary>
/// Specifies the security policy applied to OPC UA connections accepted by an OPC UA server.
/// </summary>
/// <remarks>
/// Mirrors the OPC-UA Server DataPort's <c>SecurityPolicy</c> property.
/// </remarks>
public enum OpcUaSecurityPolicy
{
    /// <summary>
    /// No message signing or encryption. Insecure; intended for test and debug scenarios only.
    /// </summary>
    None,

    /// <summary>
    /// Messages are signed using the <c>Basic256Sha256</c> policy, but not encrypted.
    /// </summary>
    Basic256Sha256Sign,

    /// <summary>
    /// Messages are signed and encrypted using the <c>Basic256Sha256</c> policy.
    /// </summary>
    Basic256Sha256SignAndEncrypt,

    /// <summary>
    /// Messages are signed and encrypted using the <c>Aes256-Sha256-RsaPss</c> policy.
    /// </summary>
    Aes256Sha256RsaPssSignAndEncrypt
}
