using ClusterManagement.Public.Connections.Contracts;
using ClusterManagement.Public.Connections.Extensions;

namespace ClusterManagement.Backend.Tests.Connections;

public class OpcUaConnectionExtensionsTests
{
    public sealed class GetCertificateStoreProperties
    {
        [Fact]
        public void Should_map_the_stores_of_a_client_connection_onto_directory_stores()
        {
            // Arrange
            var connection = new OpcUaClientConnection
            {
                ApplicationCertificatesPath = "ua-certs/0123/application",
                TrustedCertificatesPath = "ua-certs/0123/trusted",
                TrustedIssuerCertificatesPath = "ua-certs/0123/trusted-issuer",
            };

            // Act
            var properties = connection.GetCertificateStoreProperties();

            // Assert
            properties.Should().BeEquivalentTo(new Dictionary<string, object?>
            {
                ["ApplicationCertificatesStoreType"] = (byte)2,
                ["ApplicationCertificatesStorePath"] = "ua-certs/0123/application",
                ["TrustedCertificatesStoreType"] = (byte)2,
                ["TrustedCertificatesStorePath"] = "ua-certs/0123/trusted",
                ["TrustedIssuerCertificatesStoreType"] = (byte)2,
                ["TrustedIssuerCertificatesStorePath"] = "ua-certs/0123/trusted-issuer",
            });
        }

        [Fact]
        public void Should_map_the_stores_of_a_server_connection_onto_directory_stores()
        {
            // Arrange
            var connection = new OpcUaServerConnection
            {
                ApplicationCertificatesPath = "ua-certs/0123/application",
                TrustedCertificatesPath = "ua-certs/0123/trusted",
                TrustedIssuerCertificatesPath = "ua-certs/0123/trusted-issuer",
            };

            // Act
            var properties = connection.GetCertificateStoreProperties();

            // Assert
            properties.Should().BeEquivalentTo(new Dictionary<string, object?>
            {
                ["ApplicationCertificatesStoreType"] = (byte)2,
                ["ApplicationCertificatesStorePath"] = "ua-certs/0123/application",
                ["TrustedCertificatesStoreType"] = (byte)2,
                ["TrustedCertificatesStorePath"] = "ua-certs/0123/trusted",
                ["TrustedIssuerCertificatesStoreType"] = (byte)2,
                ["TrustedIssuerCertificatesStorePath"] = "ua-certs/0123/trusted-issuer",
            });
        }

        [Fact]
        public void Should_keep_absolute_paths()
        {
            // connections created before the paths were relative store absolute ones
            var path = Path.GetFullPath(Path.Combine("home", "ua-certs", "0123", "trusted"));
            var connection = new OpcUaClientConnection { TrustedCertificatesPath = path, };

            // Act
            var properties = connection.GetCertificateStoreProperties();

            // Assert
            properties["TrustedCertificatesStorePath"].Should().Be(path);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Should_map_a_store_without_path_onto_no_store(string? path)
        {
            // Arrange
            var connection = new OpcUaServerConnection
            {
                ApplicationCertificatesPath = path ?? string.Empty,
                TrustedCertificatesPath = path,
                TrustedIssuerCertificatesPath = path,
            };

            // Act
            var properties = connection.GetCertificateStoreProperties();

            // Assert
            properties["ApplicationCertificatesStoreType"].Should().Be((byte)0);
            properties["TrustedCertificatesStoreType"].Should().Be((byte)0);
            properties["TrustedIssuerCertificatesStoreType"].Should().Be((byte)0);
        }
    }
}
