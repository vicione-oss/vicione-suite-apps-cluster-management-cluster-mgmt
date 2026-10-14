using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ClusterManagement.Backend.Connections;
using ClusterManagement.Internal.Contracts;

namespace ClusterManagement.Backend.Tests.Connections;

public class OpcUaApplicationCertificateCheckTests
{
    private static OpcUaCertificateInfo Certificate(string subject, string? applicationUri)
        => new("0A1B", "Machine", subject, subject, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, applicationUri);

    public sealed class GetApplicationUri
    {
        [Fact]
        public void Should_read_the_URI_of_the_subject_alternative_name()
        {
            // Arrange
            using var key = RSA.Create(2048);
            var request = new CertificateRequest("CN=Machine", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var alternativeNames = new SubjectAlternativeNameBuilder();
            alternativeNames.AddDnsName("machine");
            alternativeNames.AddUri(new Uri("urn:machine:OPCUA:Server"));
            request.CertificateExtensions.Add(alternativeNames.Build());
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

            // Act & Assert
            OpcUaApplicationCertificateCheck.GetApplicationUri(certificate).Should().Be("urn:machine:OPCUA:Server");
        }

        [Fact]
        public void Should_return_null_without_subject_alternative_name()
        {
            // Arrange
            using var key = RSA.Create(2048);
            using var certificate = new CertificateRequest("CN=Machine", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

            // Act & Assert
            OpcUaApplicationCertificateCheck.GetApplicationUri(certificate).Should().BeNull();
        }
    }

    public sealed class GetWarnings
    {
        [Fact]
        public void Should_accept_matching_settings_in_any_order_spacing_and_casing()
            => OpcUaApplicationCertificateCheck.GetWarnings(
                    Certificate("CN=OPC-UA Server, DC=machine", "urn:machine:OPCUA:Server"), "dc=MACHINE,CN=OPC-UA Server", "urn:machine:OPCUA:Server", "machine")
                .Should().BeEmpty();

        [Fact]
        public void Should_replace_localhost_in_the_subject_but_not_in_the_application_URI_like_the_OPC_UA_stack()
            => OpcUaApplicationCertificateCheck.GetWarnings(
                    Certificate("CN=OPC-UA Server, DC=machine", "urn:localhost:OPCUA:Server"), "CN=OPC-UA Server, DC=localhost", "urn:localhost:OPCUA:Server", "machine")
                .Should().BeEmpty();

        [Fact]
        public void Should_warn_about_an_application_URI_with_the_host_name_instead_of_localhost()
            => OpcUaApplicationCertificateCheck.GetWarnings(
                    Certificate("CN=OPC-UA Server, DC=machine", "urn:machine:OPCUA:Server"), "CN=OPC-UA Server, DC=localhost", "urn:localhost:OPCUA:Server", "machine")
                .Should().Equal(OpcUaApplicationCertificateWarning.ApplicationUriMismatch);

        [Fact]
        public void Should_warn_about_a_different_subject()
            => OpcUaApplicationCertificateCheck.GetWarnings(
                    Certificate("CN=Other", "urn:machine:OPCUA:Server"), "CN=OPC-UA Server", "urn:machine:OPCUA:Server", "machine")
                .Should().Equal(OpcUaApplicationCertificateWarning.SubjectMismatch);

        [Fact]
        public void Should_warn_about_a_subject_that_is_no_distinguished_name()
            => OpcUaApplicationCertificateCheck.GetWarnings(
                    Certificate("CN=Machine", "urn:machine"), "no distinguished name ===", "urn:machine", "machine")
                .Should().Equal(OpcUaApplicationCertificateWarning.SubjectMismatch);

        [Theory]
        [InlineData("urn:other")]
        [InlineData(null)]
        public void Should_warn_about_a_different_or_missing_application_URI(string? alternativeName)
            => OpcUaApplicationCertificateCheck.GetWarnings(
                    Certificate("CN=Machine", alternativeName), "CN=Machine", "urn:machine", "machine")
                .Should().Equal(OpcUaApplicationCertificateWarning.ApplicationUriMismatch);
    }
}
