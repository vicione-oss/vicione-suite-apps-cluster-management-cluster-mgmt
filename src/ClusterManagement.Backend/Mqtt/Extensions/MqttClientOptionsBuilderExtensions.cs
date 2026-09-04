using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using MQTTnet;
using Sdk.Connections.Contracts;

namespace ClusterManagement.Backend.Mqtt.Extensions;

internal static class MqttClientOptionsBuilderExtensions
{
    public static MqttClientOptionsBuilder WithSslProtocol(this MqttClientOptionsBuilder builder, MqttConnection connection, IFileSystem fileSystem)
    {
        if (string.IsNullOrWhiteSpace(connection.ClientCertificate) && connection.SslProtocol is null)
            return builder;

        var tlsOptions = new MqttClientTlsOptions
        {
            UseTls = true,
            AllowUntrustedCertificates = connection.AllowUntrustedCertificates,
        };

        // prefer certificate over tls
        if (connection.ClientCertificate is not null)
        {
            // Accept all certs. This is not very secure, but necessary for self-signed certs
            if (tlsOptions.AllowUntrustedCertificates && !string.IsNullOrWhiteSpace(connection.ClientCertificate))
            {
                tlsOptions.CertificateValidationHandler = _ => true;
            }

            using var clientCert = CreateOrLoadClientCertificate(
                connection.ClientCertificate,
                connection.ClientCertificateKey,
                connection.ClientCertificateKeyPassword,
                fileSystem);

            if (clientCert is not null)
            {
                var pkcs12 = clientCert.Export(X509ContentType.Pkcs12);

#pragma warning disable SYSLIB0057 // Workaround for certificate not marked as exportable or persistent
                var fixedCert = new X509Certificate2(
                    pkcs12,
                    (string?)null,
                    X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet
                );
#pragma warning restore SYSLIB0057

                tlsOptions.ClientCertificatesProvider = new DefaultMqttCertificatesProvider(
                    new List<X509Certificate2> { fixedCert });
            }

            builder.WithTlsOptions(tlsOptions);

            return builder;
        }

        // We have no ClientCertificate here but tls selection
        if (connection.SslProtocol is MqttSslProtocol.Tls12 or MqttSslProtocol.Tls13)
        {
            builder.WithTlsOptions(tls =>
            {
                tls.UseTls();
                tls.WithSslProtocols(ToSslProtocols(connection.SslProtocol ?? throw new InvalidOperationException("SslProtocol error")));
            });
        }

        return builder;

        [SuppressMessage("Security", "CA5398:Avoid hardcoded SslProtocols values",
            Justification = "The TLS version is selected by the user through MqttConnection.SslProtocol; this only maps that choice onto SslProtocols.")]
        static SslProtocols ToSslProtocols(MqttSslProtocol proctocol) => proctocol switch
        {
            MqttSslProtocol.Tls12 => SslProtocols.Tls12,
            MqttSslProtocol.Tls13 => SslProtocols.Tls13,
            _ => throw new InvalidEnumArgumentException(nameof(proctocol), (int)proctocol, typeof(MqttSslProtocol)),
        };
    }

    private static X509Certificate2? CreateOrLoadClientCertificate(string clientCertificate, string? clientCertificateKey, string? clientCertificateKeyPassword, IFileSystem fileSystem)
    {
        if (fileSystem.Path.IsPathRooted(clientCertificate)
            && (string.IsNullOrEmpty(clientCertificateKey) || fileSystem.Path.IsPathRooted(clientCertificateKey)))
        {
            if (!fileSystem.File.Exists(clientCertificate))
                throw new FileNotFoundException($"Could not find certificate file in {clientCertificate}");

            if (!string.IsNullOrEmpty(clientCertificateKey) && !fileSystem.File.Exists(clientCertificateKey))
                throw new FileNotFoundException($"Could not find private key file in {clientCertificateKey}");

            return fileSystem.CreateFromPemFile(clientCertificate, clientCertificateKey, clientCertificateKeyPassword);
        }

        if (fileSystem.Path.IsPathRooted(clientCertificate) || fileSystem.Path.IsPathRooted(clientCertificateKey))
            throw new InvalidOperationException("The certificates and the certificate key must either both be a file path, or none");

        if (!string.IsNullOrWhiteSpace(clientCertificateKeyPassword))
            return X509Certificate2.CreateFromEncryptedPemFile(clientCertificate, clientCertificateKeyPassword, clientCertificateKey);

        return X509Certificate2.CreateFromPem(clientCertificate, clientCertificateKey);
    }

    private static X509Certificate2 CreateFromPemFile(this IFileSystem fileSystem, string certPemFilePath, string? keyPemFilePath = null, string? certificateKeyPassword = null)
    {
        var certContents = fileSystem.File.ReadAllText(certPemFilePath);
        var keyContents = keyPemFilePath is null ? certContents : fileSystem.File.ReadAllText(keyPemFilePath);

        if (!string.IsNullOrWhiteSpace(certificateKeyPassword))
            return X509Certificate2.CreateFromEncryptedPemFile(certContents, certificateKeyPassword, keyContents);

        return X509Certificate2.CreateFromPem(certContents, keyContents);
    }
}
