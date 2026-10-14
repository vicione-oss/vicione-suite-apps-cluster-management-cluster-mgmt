using ClusterManagement.Public.Connections.Contracts;
using ClusterManagement.Public.Connections.Extensions;
using Sdk.Connections.Contracts;

namespace ClusterManagement.Client.Tests.Connections;

public sealed class OpcUaServerConnectionTests
{
    [Fact]
    public void Should_default_to_username_and_password_authentication()
        => new OpcUaServerConnection().AuthenticationMode.Should().Be(OpcUaAuthenticationMode.UsernamePassword);

    [Fact]
    public void Should_default_to_signing_and_encrypting()
        => new OpcUaServerConnection().SecurityPolicy.Should().Be(OpcUaSecurityPolicy.Basic256Sha256SignAndEncrypt);

    [Fact]
    public void Should_keep_the_security_policy_of_a_stored_connection_without_security()
    {
        // Arrange: connections created before the default changed are stored with None
        var connection = new Connection { Id = Guid.NewGuid(), Name = "OPC/UA", Type = ConnectionType.OpcUaServer };
        connection.SetOpcUaServerConnection(new OpcUaServerConnection { SecurityPolicy = OpcUaSecurityPolicy.None });

        // Act
        var stored = connection.GetOpcUaServerConnection();

        // Assert
        stored!.SecurityPolicy.Should().Be(OpcUaSecurityPolicy.None);
    }
}
