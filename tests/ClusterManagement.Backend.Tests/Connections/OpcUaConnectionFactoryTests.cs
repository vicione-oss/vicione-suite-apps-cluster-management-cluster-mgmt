using ClusterManagement.Backend.Connections;

namespace ClusterManagement.Backend.Tests.Connections;

public class OpcUaConnectionFactoryTests
{
    private readonly OpcUaConnectionFactory _factory = new();

    public sealed class CreateClientConnection : OpcUaConnectionFactoryTests
    {
        [Fact]
        public void Should_place_the_certificate_stores_in_separate_directories_relative_to_the_workspace_home()
        {
            // Act
            var connection = _factory.CreateClientConnection();

            // Assert
            connection.ApplicationCertificatesPath.Should().MatchRegex("^ua-certs/[0-9a-f]{32}/application$");
            var directory = connection.ApplicationCertificatesPath![..^"application".Length];
            connection.TrustedCertificatesPath.Should().Be(directory + "trusted");
            connection.TrustedIssuerCertificatesPath.Should().Be(directory + "trusted-issuer");
        }

        [Fact]
        public void Should_not_share_the_certificate_directory_between_connections()
        {
            // Act
            var first = _factory.CreateClientConnection();
            var second = _factory.CreateClientConnection();

            // Assert
            first.ApplicationCertificatesPath.Should().NotBe(second.ApplicationCertificatesPath);
        }
    }

    public sealed class CreateServerConnection : OpcUaConnectionFactoryTests
    {
        [Fact]
        public void Should_place_the_certificate_stores_in_separate_directories_relative_to_the_workspace_home()
        {
            // Act
            var connection = _factory.CreateServerConnection();

            // Assert
            connection.ApplicationCertificatesPath.Should().MatchRegex("^ua-certs/[0-9a-f]{32}/application$");
            var directory = connection.ApplicationCertificatesPath[..^"application".Length];
            connection.TrustedCertificatesPath.Should().Be(directory + "trusted");
            connection.TrustedIssuerCertificatesPath.Should().Be(directory + "trusted-issuer");
        }
    }
}
