using ClusterManagement.Backend.Connections;

namespace ClusterManagement.Backend.Tests.Connections;

public class OpcUaStorePathsTests
{
    private const string Id = "0123456789abcdef0123456789abcdef";

    private static readonly string s_homeDirectory = Path.GetFullPath("home");

    public sealed class IsStorePath
    {
        [Theory]
        [InlineData("ua-certs/0123/trusted")]
        [InlineData("ua-certs/0123")]
        public void Should_accept_relative_paths_below_the_certificates_directory(string path)
            => OpcUaStorePaths.IsStorePath(path).Should().BeTrue();

        [Theory]
        [InlineData("")]
        [InlineData("ua-certs")]
        [InlineData("ua-certs/")]
        [InlineData("ua-certs-other/0123/trusted")]
        [InlineData("/ua-certs/0123/trusted")]
        [InlineData("CurrentUser\\My")]
        [InlineData("%CommonApplicationData%/opcua/cert-stores")]
        public void Should_reject_other_paths(string path)
            => OpcUaStorePaths.IsStorePath(path).Should().BeFalse();
    }

    public sealed class NormalizeStorePath
    {
        [Theory]
        [InlineData("/mnt/persistent/vicione-suite/AppData/ViciOne.Suite.ClusterManagement/ua-certs/ID/trusted", "ua-certs/ID/trusted")]
        [InlineData(@"C:\Suite\AppData\ViciOne.Suite.ClusterManagement\ua-certs\ID\application", "ua-certs/ID/application")]
        [InlineData("AppData/ViciOne.Suite.ClusterManagement/ua-certs/ID/trusted-issuer", "ua-certs/ID/trusted-issuer")]
        [InlineData("/home/ua-certs/ID/trusted/", "ua-certs/ID/trusted")]
        public void Should_make_the_store_paths_of_3_0_0_beta2_relative(string path, string expected)
            => OpcUaStorePaths.NormalizeStorePath(path.Replace("ID", Id, StringComparison.Ordinal))
                .Should().Be(expected.Replace("ID", Id, StringComparison.Ordinal));

        [Theory]
        [InlineData("ua-certs/0123/trusted")]
        [InlineData("/home/ua-certs/0123/trusted")]
        [InlineData("/home/ua-certs/ID/other")]
        [InlineData("/home/other/ID/trusted")]
        [InlineData("/home/ua-certs/ID/trusted/certs")]
        [InlineData("CurrentUser\\My")]
        [InlineData("%CommonApplicationData%/opcua/cert-stores")]
        [InlineData("")]
        [InlineData(null)]
        public void Should_keep_other_paths(string? path)
        {
            path = path?.Replace("ID", Id, StringComparison.Ordinal);

            OpcUaStorePaths.NormalizeStorePath(path).Should().Be(path);
        }
    }

    public sealed class IsApplicationStore
    {
        [Theory]
        [InlineData("ua-certs/0123/application", true)]
        [InlineData("/mnt/persistent/AppData/ua-certs/ID/application", true)]
        [InlineData("ua-certs/0123/trusted", false)]
        [InlineData("ua-certs/0123/trusted-issuer", false)]
        public void Should_tell_the_application_store_from_the_trusted_ones(string path, bool expected)
            => OpcUaStorePaths.IsApplicationStore(path.Replace("ID", Id, StringComparison.Ordinal)).Should().Be(expected);
    }

    public sealed class IsTrustedIssuerStore
    {
        [Theory]
        [InlineData("ua-certs/0123/trusted-issuer", true)]
        [InlineData("/mnt/persistent/AppData/ua-certs/ID/trusted-issuer", true)]
        [InlineData("ua-certs/0123/trusted", false)]
        [InlineData("ua-certs/0123/application", false)]
        public void Should_tell_the_trusted_issuer_store_from_the_others(string path, bool expected)
            => OpcUaStorePaths.IsTrustedIssuerStore(path.Replace("ID", Id, StringComparison.Ordinal)).Should().Be(expected);
    }

    public sealed class GetConnectionDirectory
    {
        [Theory]
        [InlineData("ua-certs/0123/trusted", "ua-certs/0123")]
        [InlineData("ua-certs/0123", "ua-certs/0123")]
        [InlineData("ua-certs/../trusted", null)]
        [InlineData("other/0123/trusted", null)]
        [InlineData("/home/ua-certs/0123/trusted", null)]
        [InlineData(null, null)]
        public void Should_return_the_directory_the_stores_of_a_connection_share(string? path, string? expected)
            => OpcUaStorePaths.GetConnectionDirectory(path).Should().Be(expected);

        [Fact]
        public void Should_return_the_directory_of_a_store_path_of_3_0_0_beta2()
            => OpcUaStorePaths.GetConnectionDirectory($"/mnt/persistent/AppData/ua-certs/{Id}/trusted").Should().Be($"ua-certs/{Id}");
    }

    public sealed class GetFullStorePath
    {
        [Fact]
        public void Should_combine_the_store_path_with_the_home_directory()
            => OpcUaStorePaths.GetFullStorePath(s_homeDirectory, "ua-certs/0123/trusted")
                .Should().Be(Path.Combine(s_homeDirectory, "ua-certs", "0123", "trusted"));

        [Theory]
        [InlineData("ua-certs/../secrets")]
        [InlineData("ua-certs/0123/../../secrets")]
        [InlineData("ua-certs/..")]
        public void Should_throw_if_the_path_leaves_the_certificates_directory(string path)
            => FluentActions.Invoking(() => OpcUaStorePaths.GetFullStorePath(s_homeDirectory, path))
                .Should().Throw<ArgumentException>();

        [Fact]
        public void Should_throw_if_the_path_is_no_store_path()
            => FluentActions.Invoking(() => OpcUaStorePaths.GetFullStorePath(s_homeDirectory, "other/trusted"))
                .Should().Throw<ArgumentException>();
    }

    public sealed class ResolveDataPortProperty
    {
        [Theory]
        [InlineData("OpcUaClientInstance", "ApplicationCertificatesStorePath")]
        [InlineData("OpcUaClientInstance", "TrustedCertificatesStorePath")]
        [InlineData("OpcUaClientInstance", "TrustedIssuerCertificatesStorePath")]
        [InlineData("OpcUaServerInstance", "ApplicationCertificatesStorePath")]
        [InlineData("OpcUaServerInstance", "TrustedCertificatesStorePath")]
        [InlineData("OpcUaServerInstance", "TrustedIssuerCertificatesStorePath")]
        public void Should_resolve_the_store_paths_of_the_OPC_UA_DataPorts(string dataPortDesignId, string propertyId)
            => OpcUaStorePaths.ResolveDataPortProperty(dataPortDesignId, propertyId, "ua-certs/0123/trusted", s_homeDirectory)
                .Should().Be(Path.Combine(s_homeDirectory, "ua-certs", "0123", "trusted"));

        [Fact]
        public void Should_resolve_a_store_path_of_3_0_0_beta2_against_the_current_home_directory()
        {
            // e.g. a cluster version created on another instance, whose home directory was elsewhere
            var path = $"/mnt/persistent/AppData/ViciOne.Suite.ClusterManagement/ua-certs/{Id}/trusted";

            OpcUaStorePaths.ResolveDataPortProperty("OpcUaServerInstance", "TrustedCertificatesStorePath", path, s_homeDirectory)
                .Should().Be(Path.Combine(s_homeDirectory, "ua-certs", Id, "trusted"));
        }

        [Theory]
        [InlineData("ua-certs/../trusted")]
        [InlineData("ua-certs/0123/../../trusted")]
        public void Should_keep_a_path_that_leads_out_of_the_certificates_directory_instead_of_failing(string path)
            => OpcUaStorePaths.ResolveDataPortProperty("OpcUaClientInstance", "TrustedCertificatesStorePath", path, s_homeDirectory)
                .Should().Be(path);

        [Fact]
        public void Should_keep_absolute_paths()
        {
            // connections created before the paths were relative store absolute ones
            var path = Path.Combine(s_homeDirectory, "ua-certs", "0123", "trusted");

            OpcUaStorePaths.ResolveDataPortProperty("OpcUaClientInstance", "TrustedCertificatesStorePath", path, s_homeDirectory)
                .Should().Be(path);
        }

        [Theory]
        [InlineData("CurrentUser\\My")]
        [InlineData("%CommonApplicationData%/opcua/cert-stores")]
        [InlineData("certs")]
        public void Should_keep_relative_paths_cluster_management_does_not_own(string path)
            => OpcUaStorePaths.ResolveDataPortProperty("OpcUaClientInstance", "TrustedCertificatesStorePath", path, s_homeDirectory)
                .Should().Be(path);

        [Fact]
        public void Should_keep_a_missing_value()
            => OpcUaStorePaths.ResolveDataPortProperty("OpcUaClientInstance", "TrustedCertificatesStorePath", null, s_homeDirectory)
                .Should().BeNull();

        [Theory]
        [InlineData("MqttClientInstance", "TrustedCertificatesStorePath")]
        [InlineData("OpcUaClientInstance", "ApplicationCertificateSubject")]
        public void Should_keep_other_properties(string dataPortDesignId, string propertyId)
            => OpcUaStorePaths.ResolveDataPortProperty(dataPortDesignId, propertyId, "ua-certs/0123/trusted", s_homeDirectory)
                .Should().Be("ua-certs/0123/trusted");
    }
}
