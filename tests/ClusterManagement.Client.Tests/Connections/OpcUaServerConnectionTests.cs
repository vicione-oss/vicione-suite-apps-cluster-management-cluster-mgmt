using ClusterManagement.Public.Connections.Contracts;

namespace ClusterManagement.Client.Tests.Connections;

public sealed class OpcUaServerConnectionTests
{
    [Fact]
    public void Should_default_to_username_and_password_authentication()
        => new OpcUaServerConnection().AuthenticationMode.Should().Be(OpcUaAuthenticationMode.UsernamePassword);

    [Fact]
    public void Should_default_to_no_security_policy()
        => new OpcUaServerConnection().SecurityPolicy.Should().Be(OpcUaSecurityPolicy.None);
}
