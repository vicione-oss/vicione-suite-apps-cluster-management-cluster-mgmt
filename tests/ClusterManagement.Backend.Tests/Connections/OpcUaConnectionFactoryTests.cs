using System.IO.Abstractions.TestingHelpers;
using ClusterManagement.Backend.Connections;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Tests.Connections;

public class OpcUaConnectionFactoryTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
    private readonly OpcUaConnectionFactory _factory;

    public OpcUaConnectionFactoryTests()
    {
        _workspaceProvider.Home.Returns("home");
        _factory = new(_fileSystem, _workspaceProvider);
    }

    public sealed class CreateClientConnection : OpcUaConnectionFactoryTests
    {
        [Fact]
        public void Should_place_the_certificate_stores_in_separate_directories_below_the_workspace_home()
        {
            // Act
            var connection = _factory.CreateClientConnection();

            // Assert
            var directory = _fileSystem.Path.GetDirectoryName(connection.ApplicationCertificatesPath)!;
            _fileSystem.Path.GetDirectoryName(directory).Should().Be(_fileSystem.Path.Combine("home", "ua-certs"));
            connection.ApplicationCertificatesPath.Should().Be(_fileSystem.Path.Combine(directory, "application"));
            connection.TrustedCertificatesPath.Should().Be(_fileSystem.Path.Combine(directory, "trusted"));
            connection.TrustedIssuerCertificatesPath.Should().Be(_fileSystem.Path.Combine(directory, "trusted-issuer"));
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
        public void Should_place_the_certificate_stores_in_separate_directories_below_the_workspace_home()
        {
            // Act
            var connection = _factory.CreateServerConnection();

            // Assert
            var directory = _fileSystem.Path.GetDirectoryName(connection.ApplicationCertificatesPath)!;
            _fileSystem.Path.GetDirectoryName(directory).Should().Be(_fileSystem.Path.Combine("home", "ua-certs"));
            connection.ApplicationCertificatesPath.Should().Be(_fileSystem.Path.Combine(directory, "application"));
            connection.TrustedCertificatesPath.Should().Be(_fileSystem.Path.Combine(directory, "trusted"));
            connection.TrustedIssuerCertificatesPath.Should().Be(_fileSystem.Path.Combine(directory, "trusted-issuer"));
        }
    }
}
