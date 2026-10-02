using ClusterManagement.Client.Connections;
using ClusterManagement.Public.Connections.Contracts;

namespace ClusterManagement.Client.Tests.Connections;

public sealed class OpcUaClientConnectionValidatorTests
{
    private static OpcUaClientConnection ValidConnection() => new()
    {
        ApplicationName = "OPC-UA Client",
        ApplicationUri = "urn:localhost:OPCUA:Client",
        Server = "localhost",
        Port = 4840,
        Endpoint = "ua/dataport",
        SubscriptionPublishingIntervalMilliseconds = 1000,
        AuthenticationMode = OpcUaAuthenticationMode.Anonymous,
        ApplicationCertificateSubject = "CN=OPC-UA Client",
        ApplicationCertificatesPath = "/var/lib/opcua/pki/own",
    };

    [Fact]
    public void Should_return_no_errors_for_a_valid_connection()
    {
        var errors = new OpcUaClientConnectionValidator().Validate(ValidConnection());

        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_require_a_server(string server)
    {
        var connection = ValidConnection();
        connection.Server = server;

        var errors = new OpcUaClientConnectionValidator().Validate(connection);

        errors.Should().ContainKey(nameof(OpcUaClientConnection.Server));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Should_reject_a_port_outside_the_valid_range(int port)
    {
        var connection = ValidConnection();
        connection.Port = port;

        var errors = new OpcUaClientConnectionValidator().Validate(connection);

        errors.Should().ContainKey(nameof(OpcUaClientConnection.Port));
    }

    [Fact]
    public void Should_require_username_and_password_when_authentication_mode_is_username_password()
    {
        var connection = ValidConnection();
        connection.AuthenticationMode = OpcUaAuthenticationMode.UsernamePassword;
        connection.Username = string.Empty;
        connection.Password = string.Empty;

        var errors = new OpcUaClientConnectionValidator().Validate(connection);

        errors.Should().ContainKey(nameof(OpcUaClientConnection.Username));
        errors.Should().ContainKey(nameof(OpcUaClientConnection.Password));
    }

    [Fact]
    public void Should_not_require_username_and_password_for_anonymous_authentication()
    {
        var connection = ValidConnection();
        connection.AuthenticationMode = OpcUaAuthenticationMode.Anonymous;
        connection.Username = string.Empty;
        connection.Password = string.Empty;

        var errors = new OpcUaClientConnectionValidator().Validate(connection);

        errors.Should().NotContainKey(nameof(OpcUaClientConnection.Username));
        errors.Should().NotContainKey(nameof(OpcUaClientConnection.Password));
    }

    [Fact]
    public void Should_not_require_certificates_paths()
    {
        var connection = ValidConnection();
        connection.ApplicationCertificatesPath = null;
        connection.TrustedCertificatesPath = null;
        connection.TrustedIssuerCertificatesPath = null;

        var errors = new OpcUaClientConnectionValidator().Validate(connection);

        errors.Should().BeEmpty();
    }
}
