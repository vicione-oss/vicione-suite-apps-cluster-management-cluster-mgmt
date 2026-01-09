using System.Runtime.InteropServices;
using AwesomeAssertions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Internal;
using Xunit;

namespace ClusterManagement.Backend.Tests.Extensions;

public class ClusterManagementOptionsExtensionsTests
{
    private readonly string _platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" : "linux";

#pragma warning disable CA1308
    private readonly string _architecture = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
#pragma warning restore CA1308

    public sealed class GetEngineHostPackageName : ClusterManagementOptionsExtensionsTests
    {
        [Fact]
        public void Uses_settings_if_newer_version_is_set()
        {
            // Arrange
            ClusterManagementOptions options = new()
            {
                EngineHostVersion = "9.8.7",
            };

            // Act
            var packageName = options.GetEngineHostPackageName();

            // Assert
            packageName.Should().Be($"9.8.7-{_platform}-{_architecture}");
        }

        [Fact]
        public void Uses_default_version_if_setting_is_older_than_default_version()
        {
            // Arrange
            ClusterManagementOptions options = new()
            {
                EngineHostVersion = "0.1.0",
            };

            // Act
            var packageName = options.GetEngineHostPackageName();

            // Assert
            packageName.Should().Be($"0.28.0-{_platform}-{_architecture}");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1.2.3.4")]
        public void Uses_default_version_if_setting_not_a_semantic_version(string? engineHostVersion)
        {
            // Arrange
            ClusterManagementOptions options = new()
            {
                EngineHostVersion = engineHostVersion,
            };

            // Act
            var packageName = options.GetEngineHostPackageName();

            // Assert
            packageName.Should().Be($"0.28.0-{_platform}-{_architecture}");
        }
    }
}
