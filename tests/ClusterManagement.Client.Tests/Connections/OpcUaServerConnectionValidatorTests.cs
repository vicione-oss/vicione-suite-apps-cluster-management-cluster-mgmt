using ClusterManagement.Client.Connections;
using ClusterManagement.Public.Connections.Contracts;

namespace ClusterManagement.Client.Tests.Connections;

public sealed class OpcUaServerConnectionValidatorTests
{
    private static OpcUaServerConnection ValidConnection() => new()
    {
        ApplicationName = "OPC-UA Server",
        ApplicationUri = "urn:localhost:OPCUA:Server",
        Namespace = "http://ifm.com/ViciOne-suite",
        NetworkInterface = "lan1",
        Endpoint = "ua/dataport",
        SecurityPolicy = OpcUaSecurityPolicy.Basic256Sha256SignAndEncrypt,
        MinPublishingIntervalMilliseconds = 100,
        MaxPublishingIntervalMilliseconds = 1000,
        AuthenticationMode = OpcUaAuthenticationMode.Anonymous,
        ApplicationCertificateSubject = "CN=OPC-UA Server, DC=localhost",
    };

    [Fact]
    public void Should_return_no_errors_for_a_valid_connection()
    {
        var errors = new OpcUaServerConnectionValidator().Validate(ValidConnection());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Should_not_require_certificates_paths()
    {
        var connection = ValidConnection();
        connection.ApplicationCertificatesPath = string.Empty;
        connection.TrustedCertificatesPath = null;
        connection.TrustedIssuerCertificatesPath = null;

        var errors = new OpcUaServerConnectionValidator().Validate(connection);

        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Should_require_a_network_interface(string networkInterface)
    {
        var connection = ValidConnection();
        connection.NetworkInterface = networkInterface;

        var errors = new OpcUaServerConnectionValidator().Validate(connection);

        errors.Should().ContainKey(nameof(OpcUaServerConnection.NetworkInterface));
    }

    [Fact]
    public void Should_require_a_namespace()
    {
        var connection = ValidConnection();
        connection.Namespace = string.Empty;

        var errors = new OpcUaServerConnectionValidator().Validate(connection);

        errors.Should().ContainKey(nameof(OpcUaServerConnection.Namespace));
    }

    [Fact]
    public void Should_reject_a_minimum_publishing_interval_greater_than_the_maximum()
    {
        var connection = ValidConnection();
        connection.MinPublishingIntervalMilliseconds = 2000;
        connection.MaxPublishingIntervalMilliseconds = 1000;

        var errors = new OpcUaServerConnectionValidator().Validate(connection);

        errors.Should().ContainKey(nameof(OpcUaServerConnection.MinPublishingIntervalMilliseconds));
    }

    [Theory]
    [InlineData(49)]
    [InlineData(60001)]
    public void Should_reject_a_minimum_publishing_interval_outside_the_dataport_range(int interval)
    {
        var connection = ValidConnection();
        connection.MinPublishingIntervalMilliseconds = interval;
        connection.MaxPublishingIntervalMilliseconds = 3600000;

        var errors = new OpcUaServerConnectionValidator().Validate(connection);

        errors.Should().ContainKey(nameof(OpcUaServerConnection.MinPublishingIntervalMilliseconds));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(3600001)]
    public void Should_reject_a_maximum_publishing_interval_outside_the_dataport_range(int interval)
    {
        var connection = ValidConnection();
        connection.MinPublishingIntervalMilliseconds = 50;
        connection.MaxPublishingIntervalMilliseconds = interval;

        var errors = new OpcUaServerConnectionValidator().Validate(connection);

        errors.Should().ContainKey(nameof(OpcUaServerConnection.MaxPublishingIntervalMilliseconds));
    }

    [Theory]
    [InlineData(50, 100)]
    [InlineData(60000, 3600000)]
    public void Should_accept_publishing_intervals_at_the_dataport_limits(int minimum, int maximum)
    {
        var connection = ValidConnection();
        connection.MinPublishingIntervalMilliseconds = minimum;
        connection.MaxPublishingIntervalMilliseconds = maximum;

        var errors = new OpcUaServerConnectionValidator().Validate(connection);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Should_require_username_and_password_when_authentication_mode_is_username_password()
    {
        var connection = ValidConnection();
        connection.AuthenticationMode = OpcUaAuthenticationMode.UsernamePassword;

        var errors = new OpcUaServerConnectionValidator().Validate(connection);

        errors.Should().ContainKey(nameof(OpcUaServerConnection.Username));
        errors.Should().ContainKey(nameof(OpcUaServerConnection.Password));
    }
}
