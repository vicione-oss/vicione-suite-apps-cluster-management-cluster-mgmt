using System.Diagnostics.CodeAnalysis;
using Sdk.Connections.Contracts;

namespace ClusterManagement.Public.Connections.Contracts;

/// <summary>
/// Represents the configuration for a client-side connection to an OPC UA server.
/// </summary>
/// <remarks>
/// Property groups mirror the OPC-UA Client DataPort's <c>Connection</c>, <c>Authentication</c>, and
/// <c>Certificate</c> property categories, so a configured connection maps directly onto the DataPort's
/// own settings.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record OpcUaClientConnection : IConnection
{
    // ---- Connection ----

    /// <summary>
    /// Gets or sets the name of the application reported to the OPC UA server when creating a session.
    /// </summary>
    public string ApplicationName { get; set; } = "OPC-UA Client";

    /// <summary>
    /// Gets or sets the URI that identifies the client application to the OPC UA server.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Not a resolvable URL, kept as a string for simple UI/JSON binding, consistent with the DataPort's ApplicationUri property.")]
    public string ApplicationUri { get; set; } = "urn:localhost:OPCUA:Client";

    /// <summary>
    /// Gets or sets the host name or IP address of the OPC UA server.
    /// </summary>
    public string Server { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the port of the OPC UA server.
    /// </summary>
    public int Port { get; set; } = 4840;

    /// <summary>
    /// Gets or sets the endpoint path on the OPC UA server, e.g. "ua/dataport".
    /// </summary>
    public string Endpoint { get; set; } = "ua/dataport";

    /// <summary>
    /// Gets or sets the requested interval, in milliseconds, at which the OPC UA server sends publish
    /// responses containing notification data to the client. Lower values increase data freshness but
    /// generate more network traffic and server load. Defaults to 1000 ms.
    /// </summary>
    public int SubscriptionPublishingIntervalMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the node id under which the OPC UA address space tree is built, e.g. "ns=2;s=MyRootFolder".
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/> or empty, the tree is built starting from the server's root/Objects folder.
    /// </remarks>
    public string? RootNodeId { get; set; }

    // ---- Authentication ----

    /// <summary>
    /// Gets or sets the authentication mode used to establish the session. Defaults to <see cref="OpcUaAuthenticationMode.Anonymous"/>.
    /// </summary>
    public OpcUaAuthenticationMode AuthenticationMode { get; set; } = OpcUaAuthenticationMode.Anonymous;

    /// <summary>
    /// Gets or sets the username for authenticating with the OPC UA server. Only used when <see cref="AuthenticationMode"/> is <see cref="OpcUaAuthenticationMode.UsernamePassword"/>.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password for authenticating with the OPC UA server. Only used when <see cref="AuthenticationMode"/> is <see cref="OpcUaAuthenticationMode.UsernamePassword"/>.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    // ---- Certificate ----

    /// <summary>
    /// Gets or sets the subject name of the application instance certificate, e.g. "CN=OPC-UA Client".
    /// </summary>
    public string ApplicationCertificateSubject { get; set; } = "CN=OPC-UA Client";

    /// <summary>
    /// Gets or sets the path of the directory the client's application instance certificate is loaded from.
    /// </summary>
    /// <remarks>
    /// Maps to the DataPort's <c>ApplicationCertificatesStorePath</c>. When set, <c>ApplicationCertificatesStoreType</c> is
    /// <c>Directory</c>; when <see langword="null"/> or empty, it is <c>None</c>.
    /// </remarks>
    public string? ApplicationCertificatesPath { get; set; }

    /// <summary>
    /// Gets or sets the path of the directory trusted server certificates are loaded from.
    /// </summary>
    /// <remarks>
    /// Maps to the DataPort's <c>TrustedCertificatesStorePath</c>. When set, <c>TrustedCertificatesStoreType</c> is
    /// <c>Directory</c>; when <see langword="null"/> or empty, it is <c>None</c>.
    /// </remarks>
    public string? TrustedCertificatesPath { get; set; }

    /// <summary>
    /// Gets or sets the path of the directory trusted issuer (CA) certificates are loaded from.
    /// </summary>
    /// <remarks>
    /// Maps to the DataPort's <c>TrustedIssuerCertificatesStorePath</c>. When set, <c>TrustedIssuerCertificatesStoreType</c>
    /// is <c>Directory</c>; when <see langword="null"/> or empty, it is <c>None</c>.
    /// </remarks>
    public string? TrustedIssuerCertificatesPath { get; set; }

    /// <summary>
    /// Gets or sets a value which determines whether the client should automatically accept untrusted or self-signed server certificates when establishing a secure connection.
    /// </summary>
    public bool AutoAcceptUntrustedCertificates { get; set; }
}
