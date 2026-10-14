using ClusterManagement.Backend.Connections;
using ClusterManagement.Public.Connections.Contracts;

namespace ClusterManagement.Backend.Tests.Connections;

public class OpcUaConnectionSerializerTests
{
    private const string Id = "0123456789abcdef0123456789abcdef";

    public sealed class Deserialize
    {
        [Fact]
        public void Should_make_the_store_paths_of_a_client_connection_of_3_0_0_beta2_relative()
        {
            // Arrange
            var serializer = new OpcUaConnectionSerializer<OpcUaClientConnection>();
            var json = serializer.Serialize(new OpcUaClientConnection
            {
                Server = "plc",
                ApplicationCertificatesPath = $"/mnt/persistent/AppData/ua-certs/{Id}/application",
                TrustedCertificatesPath = $"/mnt/persistent/AppData/ua-certs/{Id}/trusted",
                TrustedIssuerCertificatesPath = $"/mnt/persistent/AppData/ua-certs/{Id}/trusted-issuer",
            });

            // Act
            var connection = serializer.Deserialize(json);

            // Assert
            connection.Should().BeEquivalentTo(new OpcUaClientConnection
            {
                Server = "plc",
                ApplicationCertificatesPath = $"ua-certs/{Id}/application",
                TrustedCertificatesPath = $"ua-certs/{Id}/trusted",
                TrustedIssuerCertificatesPath = $"ua-certs/{Id}/trusted-issuer",
            });
        }

        [Fact]
        public void Should_make_the_store_paths_of_a_server_connection_of_3_0_0_beta2_relative()
        {
            // Arrange
            var serializer = new OpcUaConnectionSerializer<OpcUaServerConnection>();
            var json = serializer.Serialize(new OpcUaServerConnection
            {
                NetworkInterface = "lan1",
                ApplicationCertificatesPath = $@"C:\Suite\AppData\ua-certs\{Id}\application",
                TrustedCertificatesPath = $@"C:\Suite\AppData\ua-certs\{Id}\trusted",
                TrustedIssuerCertificatesPath = null,
            });

            // Act
            var connection = serializer.Deserialize(json);

            // Assert
            connection.Should().BeEquivalentTo(new OpcUaServerConnection
            {
                NetworkInterface = "lan1",
                ApplicationCertificatesPath = $"ua-certs/{Id}/application",
                TrustedCertificatesPath = $"ua-certs/{Id}/trusted",
                TrustedIssuerCertificatesPath = null,
            });
        }

        [Fact]
        public void Should_keep_relative_store_paths()
        {
            // Arrange
            var serializer = new OpcUaConnectionSerializer<OpcUaServerConnection>();
            var original = new OpcUaServerConnection
            {
                ApplicationCertificatesPath = $"ua-certs/{Id}/application",
                TrustedCertificatesPath = $"ua-certs/{Id}/trusted",
                TrustedIssuerCertificatesPath = $"ua-certs/{Id}/trusted-issuer",
            };

            // Act
            var connection = serializer.Deserialize(serializer.Serialize(original));

            // Assert
            connection.Should().BeEquivalentTo(original);
        }
    }
}
