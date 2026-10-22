using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ClusterManagement.Internal.Extensions;
using MQTTnet;
using Sdk.Connections.Contracts;

namespace ClusterManagement.Internal.Tests.Extensions;

public class MqttClientOptionsBuilderExtensionsTests
{
    private const string KeyPassword = "secret";

    // Generating an RSA key takes long, so all tests share one client certificate.
    private static readonly ClientCertificatePem s_clientCertificate = ClientCertificatePem.Create();

    private static MqttConnection CreateConnection() => new()
    {
        Address = "broker.local",
        Port = 1883,
        Protocol = MqttConnectionType.TCP,
        ProtocolVersion = MqttProtocolVersion.V500,
    };

    private static MqttClientOptions Build(MqttConnection connection, IFileSystem? fileSystem = null)
        => new MqttClientOptionsBuilder()
            .WithSuiteConnection(connection, fileSystem ?? new MockFileSystem())
            .Build();

    private sealed record ClientCertificatePem(string Certificate, string Key, string EncryptedKey)
    {
        public static ClientCertificatePem Create()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=client", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

            return new ClientCertificatePem(
                certificate.ExportCertificatePem(),
                rsa.ExportPkcs8PrivateKeyPem(),
                rsa.ExportEncryptedPkcs8PrivateKeyPem(KeyPassword,
                    new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 10_000)));
        }
    }

    public sealed class WithTimingSettings : MqttClientOptionsBuilderExtensionsTests
    {
        [Fact]
        public void Should_apply_keep_alive_and_connect_timeout()
        {
            // Arrange
            var connection = CreateConnection() with { KeepAliveSeconds = 45, ConnectTimeoutSeconds = 12 };

            // Act
            var options = Build(connection);

            // Assert
            options.KeepAlivePeriod.Should().Be(TimeSpan.FromSeconds(45));
            options.Timeout.Should().Be(TimeSpan.FromSeconds(12));
        }

        [Fact]
        public void Should_turn_keep_alive_off_for_0()
        {
            // Arrange
            var connection = CreateConnection() with { KeepAliveSeconds = 0 };

            // Act
            var options = Build(connection);

            // Assert
            options.KeepAlivePeriod.Should().Be(TimeSpan.Zero);
        }
    }

    [SuppressMessage("Security", "CA5398:Avoid hardcoded SslProtocols values",
        Justification = "The tests check that the TLS version selected through MqttConnection.SslProtocol is applied.")]
    public sealed class WithTlsSettings : MqttClientOptionsBuilderExtensionsTests
    {
        [Fact]
        public void Should_not_use_tls_without_ssl_protocol()
        {
            // Arrange
            var connection = CreateConnection() with { ClientCertificate = "left over from a previous TLS setting" };

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.TlsOptions.UseTls.Should().BeFalse();
        }

        [Fact]
        public void Should_apply_the_selected_ssl_protocol()
        {
            // Arrange
            var connection = CreateConnection() with { SslProtocol = MqttSslProtocol.Tls13 };

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.TlsOptions.UseTls.Should().BeTrue();
            options.ChannelOptions.TlsOptions.SslProtocol.Should().Be(SslProtocols.Tls13);
            options.ChannelOptions.TlsOptions.CertificateValidationHandler.Should().BeNull();
        }

        [Fact]
        public void Should_accept_untrusted_certificates_without_a_client_certificate()
        {
            // Arrange
            var connection = CreateConnection() with { SslProtocol = MqttSslProtocol.Tls12, AllowUntrustedCertificates = true };

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.TlsOptions.AllowUntrustedCertificates.Should().BeTrue();
            options.ChannelOptions.TlsOptions.CertificateValidationHandler.Should().NotBeNull();
        }

        [Fact]
        public void Should_apply_the_ssl_protocol_and_the_client_certificate()
        {
            // Arrange
            var connection = CreateConnection() with
            {
                SslProtocol = MqttSslProtocol.Tls12,
                ClientCertificate = s_clientCertificate.Certificate,
                ClientCertificateKey = s_clientCertificate.Key,
            };

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.TlsOptions.SslProtocol.Should().Be(SslProtocols.Tls12);
            options.ChannelOptions.TlsOptions.ClientCertificatesProvider.Should().NotBeNull();
            options.ChannelOptions.TlsOptions.ClientCertificatesProvider!.GetCertificates().Count.Should().Be(1);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Should_load_an_encrypted_client_key(bool fromFiles)
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var certificatePath = fileSystem.Path.GetFullPath("/certs/client.pem");
            var keyPath = fileSystem.Path.GetFullPath("/certs/client.key");
            fileSystem.AddFile(certificatePath, new MockFileData(s_clientCertificate.Certificate));
            fileSystem.AddFile(keyPath, new MockFileData(s_clientCertificate.EncryptedKey));

            var connection = CreateConnection() with
            {
                SslProtocol = MqttSslProtocol.Tls12,
                ClientCertificate = fromFiles ? certificatePath : s_clientCertificate.Certificate,
                ClientCertificateKey = fromFiles ? keyPath : s_clientCertificate.EncryptedKey,
                ClientCertificateKeyPassword = KeyPassword,
            };

            // Act
            var options = Build(connection, fileSystem);

            // Assert
            options.ChannelOptions.TlsOptions.ClientCertificatesProvider!.GetCertificates().Count.Should().Be(1);
        }

        [Fact]
        public void Should_validate_the_broker_certificate_with_a_client_certificate()
        {
            // Arrange
            var connection = CreateConnection() with
            {
                SslProtocol = MqttSslProtocol.Tls12,
                ClientCertificate = s_clientCertificate.Certificate,
                ClientCertificateKey = s_clientCertificate.Key,
            };

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.TlsOptions.AllowUntrustedCertificates.Should().BeFalse();
            options.ChannelOptions.TlsOptions.CertificateValidationHandler.Should().BeNull();
        }

        [Theory]
        [InlineData(false, null)]
        [InlineData(true, null)]
        [InlineData(true, " ")]
        public void Should_load_the_key_from_the_certificate_without_a_key(bool fromFile, string? key)
        {
            // Arrange
            var combinedPem = s_clientCertificate.Certificate + "\n" + s_clientCertificate.Key;

            var fileSystem = new MockFileSystem();
            var certificatePath = fileSystem.Path.GetFullPath("/certs/client.pem");
            fileSystem.AddFile(certificatePath, new MockFileData(combinedPem));

            var connection = CreateConnection() with
            {
                SslProtocol = MqttSslProtocol.Tls12,
                ClientCertificate = fromFile ? certificatePath : combinedPem,
                ClientCertificateKey = key,
            };

            // Act
            var options = Build(connection, fileSystem);

            // Assert
            options.ChannelOptions.TlsOptions.ClientCertificatesProvider!.GetCertificates().Count.Should().Be(1);
        }

        [Fact]
        public void Should_refuse_a_certificate_path_with_key_content()
        {
            // Arrange
            var fileSystem = new MockFileSystem();
            var certificatePath = fileSystem.Path.GetFullPath("/certs/client.pem");
            fileSystem.AddFile(certificatePath, new MockFileData(s_clientCertificate.Certificate));

            var connection = CreateConnection() with
            {
                SslProtocol = MqttSslProtocol.Tls12,
                ClientCertificate = certificatePath,
                ClientCertificateKey = s_clientCertificate.Key,
            };

            // Act
            var build = () => Build(connection, fileSystem);

            // Assert
            build.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Should_ignore_an_empty_client_certificate()
        {
            // Arrange
            var connection = CreateConnection() with { SslProtocol = MqttSslProtocol.Tls12, ClientCertificate = string.Empty };

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.TlsOptions.UseTls.Should().BeTrue();
            options.ChannelOptions.TlsOptions.ClientCertificatesProvider.Should().BeNull();
        }

        [Fact]
        public void Should_connect_a_tcp_with_tls_connection_over_tcp_with_tls()
        {
            // Arrange
#pragma warning disable CS0618 // Connections stored before SslProtocol existed still carry TCPWithTLS.
            var connection = CreateConnection() with { Protocol = MqttConnectionType.TCPWithTLS };
#pragma warning restore CS0618

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.Should().BeOfType<MqttClientTcpOptions>();
            options.ChannelOptions.TlsOptions.UseTls.Should().BeTrue();
        }
    }

    public sealed class WithWebSocketSettings : MqttClientOptionsBuilderExtensionsTests
    {
        [Theory]
        [InlineData("broker.local", null, "ws://broker.local:9001/")]
        [InlineData("http://broker.local/mqtt", null, "ws://broker.local:9001/mqtt")]
        [InlineData("https://broker.local/mqtt", null, "wss://broker.local:9001/mqtt")]
        [InlineData("ws://broker.local:8080/mqtt", null, "ws://broker.local:9001/mqtt")]
        [InlineData("ws://broker.local/mqtt", MqttSslProtocol.Tls12, "wss://broker.local:9001/mqtt")]
        public void Should_build_the_websocket_uri_from_address_port_and_tls(string address, MqttSslProtocol? sslProtocol, string expected)
        {
            // Arrange
            var connection = CreateConnection() with
            {
                Protocol = MqttConnectionType.WebSocket,
                Address = address,
                Port = 9001,
                SslProtocol = sslProtocol,
            };

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.Should().BeOfType<MqttClientWebSocketOptions>().Which.Uri.Should().Be(expected);
        }

        [Fact]
        public void Should_pass_the_tls_options_to_the_websocket_channel()
        {
            // Arrange
            var connection = CreateConnection() with
            {
                Protocol = MqttConnectionType.WebSocket,
                SslProtocol = MqttSslProtocol.Tls12,
                AllowUntrustedCertificates = true,
            };

            // Act
            var options = Build(connection);

            // Assert
            var tlsOptions = options.ChannelOptions.Should().BeOfType<MqttClientWebSocketOptions>().Which.TlsOptions;
            tlsOptions.UseTls.Should().BeTrue();
            tlsOptions.CertificateValidationHandler.Should().NotBeNull();
        }
    }

    public sealed class WithWillSettings : MqttClientOptionsBuilderExtensionsTests
    {
        [Fact]
        public void Should_apply_will_options()
        {
            // Arrange
            var connection = CreateConnection() with { WillTopic = "status/offline", WillMessage = "gone", WillRetain = true };

            // Act
            var options = Build(connection);

            // Assert
            options.WillTopic.Should().Be("status/offline");
            options.WillPayload.Should().Equal("gone"u8.ToArray());
            options.WillRetain.Should().BeTrue();
        }

        [Fact]
        public void Should_apply_will_options_with_an_empty_message()
        {
            // Arrange
            var connection = CreateConnection() with { WillTopic = "status/offline", WillMessage = string.Empty };

            // Act
            var options = Build(connection);

            // Assert
            options.WillTopic.Should().Be("status/offline");
            // MQTTnet keeps an empty payload as null; the topic decides whether the will is sent.
            (options.WillPayload ?? []).Should().BeEmpty();
        }

        [Fact]
        public void Should_not_apply_will_options_without_a_topic()
        {
            // Arrange
            var connection = CreateConnection() with { WillMessage = "gone" };

            // Act
            var options = Build(connection);

            // Assert
            options.WillTopic.Should().BeNull();
        }
    }
}
