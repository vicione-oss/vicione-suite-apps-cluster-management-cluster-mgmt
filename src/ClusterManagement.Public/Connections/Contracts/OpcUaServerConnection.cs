using System.Diagnostics.CodeAnalysis;
using Sdk.Connections.Contracts;

namespace ClusterManagement.Public.Connections.Contracts;

/// <summary>
/// Represents the configuration for a server-side OPC UA connection, i.e. an OPC UA server endpoint
/// that clients connect to.
/// </summary>
/// <remarks>
/// Property groups mirror the OPC-UA Server DataPort's <c>Connection</c>, <c>Authentication</c>, and
/// <c>Certificate</c> property categories, so a configured connection maps directly onto the DataPort's
/// own settings.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record OpcUaServerConnection : IConnection
{
    /// <summary>
    /// The <see cref="NetworkInterface"/> name of the local loopback interface.
    /// </summary>
    public const string LocalNetworkInterface = "local";

    /// <summary>
    /// The smallest <see cref="MinPublishingIntervalMilliseconds"/> the DataPort accepts.
    /// </summary>
    public const int MinPublishingIntervalLowerLimitMilliseconds = 50;

    /// <summary>
    /// The largest <see cref="MinPublishingIntervalMilliseconds"/> the DataPort accepts.
    /// </summary>
    public const int MinPublishingIntervalUpperLimitMilliseconds = 60000;

    /// <summary>
    /// The smallest <see cref="MaxPublishingIntervalMilliseconds"/> the DataPort accepts.
    /// </summary>
    public const int MaxPublishingIntervalLowerLimitMilliseconds = 100;

    /// <summary>
    /// The largest <see cref="MaxPublishingIntervalMilliseconds"/> the DataPort accepts.
    /// </summary>
    public const int MaxPublishingIntervalUpperLimitMilliseconds = 3600000;

    // ---- Connection ----

    /// <summary>
    /// Gets or sets the name of the server application reported to connecting clients.
    /// </summary>
    public string ApplicationName { get; set; } = "OPC-UA Server";

    /// <summary>
    /// Gets or sets the URI that identifies the server application to connecting clients.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Not a resolvable URL, kept as a string for simple UI/JSON binding, consistent with the DataPort's ApplicationUri property.")]
    public string ApplicationUri { get; set; } = "urn:localhost:OPCUA:Server";

    /// <summary>
    /// Gets or sets the OPC UA namespace URI the server publishes its address space under.
    /// </summary>
    public string Namespace { get; set; } = "http://ifm.com/ViciOne-suite";

    /// <summary>
    /// Gets or sets the name of the host's network interface the server binds to, e.g. "lan1".
    /// </summary>
    /// <remarks>
    /// Binding to all interfaces is not supported, so a connection must name exactly one interface.
    /// <see cref="LocalNetworkInterface"/> names the local loopback interface.
    /// </remarks>
    public string NetworkInterface { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the port the server listens on.
    /// </summary>
    /// <remarks>
    /// Not editable in the connection settings dialog, so it is always the standard OPC UA port 4840.
    /// </remarks>
    public int Port { get; set; } = 4840;

    /// <summary>
    /// Gets or sets the endpoint path the server exposes, e.g. "ua/dataport".
    /// </summary>
    public string Endpoint { get; set; } = "ua/dataport";

    /// <summary>
    /// Gets or sets a value which determines whether custom transport quotas are applied to harden the
    /// server against denial-of-service attacks.
    /// </summary>
    /// <remarks>
    /// Maps to the OPC-UA Server DataPort's <c>TransportQuotas</c> property.
    /// </remarks>
    public bool UseCustomTransportQuotas { get; set; } = true;

    /// <summary>
    /// Gets or sets the security policy applied to connections accepted by the server.
    /// Defaults to <see cref="OpcUaSecurityPolicy.None"/>.
    /// </summary>
    public OpcUaSecurityPolicy SecurityPolicy { get; set; } = OpcUaSecurityPolicy.None;

    /// <summary>
    /// Gets or sets the minimum publishing interval, in milliseconds, the server accepts from a client
    /// subscription request. Requests below this value are clamped to it. Lower values allow more
    /// responsive subscriptions but increase CPU and network utilization. Must be between
    /// <see cref="MinPublishingIntervalLowerLimitMilliseconds"/> and <see cref="MinPublishingIntervalUpperLimitMilliseconds"/>.
    /// Defaults to 100 ms.
    /// </summary>
    public int MinPublishingIntervalMilliseconds { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum publishing interval, in milliseconds, the server accepts from a client
    /// subscription request. Requests above this value are clamped to it. Higher values reduce server
    /// load but increase data latency. Must be between <see cref="MaxPublishingIntervalLowerLimitMilliseconds"/>
    /// and <see cref="MaxPublishingIntervalUpperLimitMilliseconds"/>. Defaults to 1000 ms.
    /// </summary>
    public int MaxPublishingIntervalMilliseconds { get; set; } = 1000;

    // ---- Authentication ----

    /// <summary>
    /// Gets or sets the authentication mode the server requires from connecting clients. Defaults to
    /// <see cref="OpcUaAuthenticationMode.UsernamePassword"/>, matching the DataPort.
    /// </summary>
    /// <remarks>
    /// The DataPort refuses anonymous sessions unless the server is configured for them, and a server set to
    /// <see cref="OpcUaAuthenticationMode.UsernamePassword"/> without <see cref="Username"/> and <see cref="Password"/> refuses to start.
    /// </remarks>
    public OpcUaAuthenticationMode AuthenticationMode { get; set; } = OpcUaAuthenticationMode.UsernamePassword;

    /// <summary>
    /// Gets or sets the username a client must supply. Only used when <see cref="AuthenticationMode"/> is <see cref="OpcUaAuthenticationMode.UsernamePassword"/>.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password a client must supply. Only used when <see cref="AuthenticationMode"/> is <see cref="OpcUaAuthenticationMode.UsernamePassword"/>.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    // ---- Certificate ----

    /// <summary>
    /// Gets or sets the subject name of the server's application instance certificate, e.g. "CN=OPC-UA Server, DC=localhost".
    /// </summary>
    public string ApplicationCertificateSubject { get; set; } = "CN=OPC-UA Server, DC=localhost";

    /// <summary>
    /// Gets or sets the path of the directory the server's application instance certificate is loaded from.
    /// </summary>
    /// <remarks>
    /// Not editable in the connection settings dialog, a new connection gets a directory of its own below the workspace home.
    /// Maps to the DataPort's <c>ApplicationCertificatesStorePath</c>, with <c>ApplicationCertificatesStoreType</c> set to <c>Directory</c>.
    /// </remarks>
    public string ApplicationCertificatesPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path of the directory trusted client certificates are loaded from.
    /// </summary>
    /// <remarks>
    /// Not editable in the connection settings dialog, a new connection gets a directory of its own below the workspace home.
    /// Maps to the DataPort's <c>TrustedCertificatesStorePath</c>. When set, <c>TrustedCertificatesStoreType</c> is
    /// <c>Directory</c>; when <see langword="null"/> or empty, it is <c>None</c>.
    /// </remarks>
    public string? TrustedCertificatesPath { get; set; }

    /// <summary>
    /// Gets or sets the path of the directory trusted issuer (CA) certificates are loaded from.
    /// </summary>
    /// <remarks>
    /// Not editable in the connection settings dialog, a new connection gets a directory of its own below the workspace home.
    /// Maps to the DataPort's <c>TrustedIssuerCertificatesStorePath</c>. When set, <c>TrustedIssuerCertificatesStoreType</c>
    /// is <c>Directory</c>; when <see langword="null"/> or empty, it is <c>None</c>.
    /// </remarks>
    public string? TrustedIssuerCertificatesPath { get; set; }

    /// <summary>
    /// Gets or sets a value which determines whether the server automatically accepts untrusted or
    /// self-signed client certificates when establishing a secure connection.
    /// </summary>
    public bool AutoAcceptUntrustedCertificates { get; set; }
}
