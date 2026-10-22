using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using MQTTnet;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;

namespace ClusterManagement.Internal.Extensions;

/// <summary>
/// Copy of suite internal extension methods for <see cref="MqttClientOptionsBuilder"/> to support MQTT connections in the cluster management backend and client.
/// Keep it in line with the suite's <c>Core.Shared/Extensions/MqttClientOptionsBuilderExtensions.cs</c>.
/// </summary>
public static class MqttClientOptionsBuilderExtensions
{
    /// <summary>
    /// Applies all settings of <paramref name="connection"/>, its TLS settings included. Broker certificates are only accepted
    /// without validation when <see cref="MqttConnection.AllowUntrustedCertificates"/> is set. A client certificate and key given
    /// as file paths are read through <paramref name="fileSystem"/>.
    /// </summary>
    public static MqttClientOptionsBuilder WithSuiteConnection(this MqttClientOptionsBuilder builder, MqttConnection connection, IFileSystem fileSystem)
    {
        if (!string.IsNullOrWhiteSpace(connection.Username))
        {
            if (string.IsNullOrEmpty(connection.Password))
                builder.WithCredentials(connection.Username);
            else
                builder.WithCredentials(connection.Username, connection.Password);
        }

        if (!string.IsNullOrWhiteSpace(connection.ClientId))
            builder.WithClientId(connection.ClientId);

        // A keep-alive of 0 turns keep-alive off. MQTTnet waits the timeout for every response from the broker, not only for
        // the connection.
        builder
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(connection.KeepAliveSeconds))
            .WithTimeout(TimeSpan.FromSeconds(connection.ConnectTimeoutSeconds));

        var useTls = connection.UsesTls();

        builder
            .WithCleanSession(connection.CleanSession)
            .WithProtocolAndVersion(connection, useTls)
            .WithWillOptions(connection);

        if (useTls)
            builder.WithTlsOptions(CreateTlsOptions(connection, fileSystem));

        return builder;
    }

    /// <summary>
    /// Creates the TLS options from the selected TLS version, <see cref="MqttConnection.AllowUntrustedCertificates"/> and the
    /// client certificate. Over WebSocket MQTTnet does not apply the TLS version; the operating system chooses it.
    /// </summary>
    private static MqttClientTlsOptions CreateTlsOptions(MqttConnection connection, IFileSystem fileSystem)
    {
        var tlsOptions = new MqttClientTlsOptions
        {
            UseTls = true,
            AllowUntrustedCertificates = connection.AllowUntrustedCertificates,
        };

        if (connection.SslProtocol is { } sslProtocol)
            tlsOptions.SslProtocol = ToSslProtocols(sslProtocol);

        // Accepts any broker certificate. This is not very secure, but necessary for self-signed certificates.
        if (connection.AllowUntrustedCertificates)
            tlsOptions.CertificateValidationHandler = _ => true;

        if (!string.IsNullOrWhiteSpace(connection.ClientCertificate))
        {
            using var clientCert = CreateOrLoadClientCertificate(
                connection.ClientCertificate,
                connection.ClientCertificateKey,
                connection.ClientCertificateKeyPassword,
                fileSystem);

            // SChannel refuses the ephemeral key of a PEM certificate. A PKCS#12 import gives it a key that is not ephemeral,
            // whose key file is removed again when the certificate is disposed or finalized.
            var fixedCert = X509CertificateLoader.LoadPkcs12(clientCert.Export(X509ContentType.Pkcs12), password: null);

            tlsOptions.ClientCertificatesProvider = new DefaultMqttCertificatesProvider(
                new List<X509Certificate2> { fixedCert });
        }

        return tlsOptions;

        [SuppressMessage("Security", "CA5398:Avoid hardcoded SslProtocols values",
            Justification = "The TLS version is selected by the user through MqttConnection.SslProtocol; this only maps that choice onto SslProtocols.")]
        static SslProtocols ToSslProtocols(MqttSslProtocol protocol) => protocol switch
        {
            MqttSslProtocol.Tls12 => SslProtocols.Tls12,
            MqttSslProtocol.Tls13 => SslProtocols.Tls13,
            _ => throw new InvalidEnumArgumentException(nameof(protocol), (int)protocol, typeof(MqttSslProtocol)),
        };
    }

    /// <summary>
    /// The settings UI hides the certificate fields when no TLS version is selected, so only <see cref="MqttConnection.SslProtocol"/>
    /// and the obsolete <see cref="MqttConnectionType.TCPWithTLS"/> switch TLS on.
    /// </summary>
    private static bool UsesTls(this MqttConnection connection)
#pragma warning disable CS0618 // Connections stored before SslProtocol existed still carry TCPWithTLS.
        => connection.SslProtocol is not null || connection.Protocol == MqttConnectionType.TCPWithTLS;
#pragma warning restore CS0618

    private static X509Certificate2 CreateOrLoadClientCertificate(string clientCertificate, string? clientCertificateKey, string? clientCertificateKeyPassword, IFileSystem fileSystem)
    {
        if (string.IsNullOrWhiteSpace(clientCertificateKey))
            clientCertificateKey = null;

        if (fileSystem.Path.IsPathRooted(clientCertificate)
            && (clientCertificateKey is null || fileSystem.Path.IsPathRooted(clientCertificateKey)))
        {
            if (!fileSystem.File.Exists(clientCertificate))
                throw new FileNotFoundException($"Could not find certificate file in {clientCertificate}");

            if (clientCertificateKey is not null && !fileSystem.File.Exists(clientCertificateKey))
                throw new FileNotFoundException($"Could not find private key file in {clientCertificateKey}");

            return fileSystem.CreateFromPemFile(clientCertificate, clientCertificateKey, clientCertificateKeyPassword);
        }

        if (fileSystem.Path.IsPathRooted(clientCertificate) || fileSystem.Path.IsPathRooted(clientCertificateKey))
            throw new InvalidOperationException("The certificates and the certificate key must either both be a file path, or none");

        return CreateFromPem(clientCertificate, clientCertificateKey ?? clientCertificate, clientCertificateKeyPassword);
    }

    private static X509Certificate2 CreateFromPemFile(this IFileSystem fileSystem, string certPemFilePath, string? keyPemFilePath = null, string? certificateKeyPassword = null)
    {
        var certContents = fileSystem.File.ReadAllText(certPemFilePath);
        var keyContents = string.IsNullOrWhiteSpace(keyPemFilePath) ? certContents : fileSystem.File.ReadAllText(keyPemFilePath);

        return CreateFromPem(certContents, keyContents, certificateKeyPassword);
    }

    /// <summary>
    /// Takes PEM content; the <c>*PemFile</c> variants of <see cref="X509Certificate2"/> read from the real file system.
    /// </summary>
    private static X509Certificate2 CreateFromPem(string certPem, string keyPem, string? keyPassword)
        => string.IsNullOrWhiteSpace(keyPassword)
            ? X509Certificate2.CreateFromPem(certPem, keyPem)
            : X509Certificate2.CreateFromEncryptedPem(certPem, keyPem, keyPassword);

    private static MqttClientOptionsBuilder WithProtocolAndVersion(this MqttClientOptionsBuilder builder, MqttConnection connection, bool useTls)
    {
        switch (connection.Protocol)
        {
            case MqttConnectionType.TCP:
#pragma warning disable CS0618 // Connections stored before SslProtocol existed still carry TCPWithTLS.
            case MqttConnectionType.TCPWithTLS:
#pragma warning restore CS0618
                builder.WithTcpServer(connection.Address, connection.Port);
                break;
            case MqttConnectionType.WebSocket:
                builder.WithWebSocketServer(b => b.WithUri(GetWebSocketUri(connection, useTls)));
                break;
            default:
                throw new InvalidEnumArgumentException(nameof(connection.Protocol),
                    (int)connection.Protocol,
                    typeof(MqttConnectionType));
        }

        builder.WithProtocolVersion(ToMqttProtocolVersion(connection.ProtocolVersion));

        return builder;

        static MQTTnet.Formatter.MqttProtocolVersion ToMqttProtocolVersion(MqttProtocolVersion protocolVersion) => protocolVersion switch
        {
            MqttProtocolVersion.V311 => MQTTnet.Formatter.MqttProtocolVersion.V311,
            MqttProtocolVersion.V500 => MQTTnet.Formatter.MqttProtocolVersion.V500,
            _ => throw new InvalidEnumArgumentException(nameof(protocolVersion), (int)protocolVersion, typeof(MqttProtocolVersion)),
        };
    }

    /// <summary>
    /// <see cref="MqttConnectionExtensions.GetWebsocketUri"/> maps only <c>http</c> to <c>ws</c>; the WebSocket client accepts
    /// nothing but <c>ws</c> and <c>wss</c>, and a selected TLS version has to end up as <c>wss</c>.
    /// </summary>
    private static string GetWebSocketUri(MqttConnection connection, bool useTls)
    {
        var uriBuilder = new UriBuilder(connection.GetWebsocketUri());
        var isSecureScheme = uriBuilder.Scheme is "https" or "wss";

        uriBuilder.Scheme = useTls || isSecureScheme ? "wss" : "ws";

        return uriBuilder.Uri.ToString();
    }

    /// <summary>
    /// An empty <see cref="MqttConnection.WillMessage"/> is sent as an empty payload, which MQTT allows.
    /// </summary>
    private static MqttClientOptionsBuilder WithWillOptions(this MqttClientOptionsBuilder builder, MqttConnection connection)
    {
        if (string.IsNullOrWhiteSpace(connection.WillTopic))
            return builder;

        builder.WithWillPayload(connection.WillMessage ?? string.Empty);
        builder.WithWillTopic(connection.WillTopic);
        builder.WithWillRetain(connection.WillRetain);
        builder.WithWillQualityOfServiceLevel(ToMqttQualityOfServiceLevel(connection.QualityOfService));

        return builder;

        static MQTTnet.Protocol.MqttQualityOfServiceLevel ToMqttQualityOfServiceLevel(MqttQualityOfServiceLevel serviceLevel) => serviceLevel switch
        {
            MqttQualityOfServiceLevel.AtMostOnce => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce,
            MqttQualityOfServiceLevel.AtLeastOnce => MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce,
            MqttQualityOfServiceLevel.ExactlyOnce => MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce,
            _ => throw new InvalidEnumArgumentException(nameof(serviceLevel), (int)serviceLevel, typeof(MqttQualityOfServiceLevel)),
        };
    }
}
