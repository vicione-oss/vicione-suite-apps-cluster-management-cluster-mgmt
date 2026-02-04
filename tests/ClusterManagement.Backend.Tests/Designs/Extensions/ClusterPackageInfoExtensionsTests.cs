using AwesomeAssertions;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Internal.Contracts;
using Xunit;

namespace ClusterManagement.Backend.Tests.Designs.Extensions;

public class ClusterPackageInfoExtensionsTests
{
    public sealed class GetHighestAvailableVersion : ClusterPackageInfoExtensionsTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Returns_null_when_InstalledVersion_is(string? value)
        {
            // Arrange
            var packageInfo = new ClusterPackageInfo
            {
                Name = "TestPackage",
                InstalledVersion = value,
                Versions = [new ClusterPackageVersion("1.0.0"), new ClusterPackageVersion("2.0.0")]
            };

            // Act
            var result = packageInfo.GetHighestAvailableVersion();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Returns_null_when_Versions_is_null()
        {
            // Arrange
            var packageInfo = new ClusterPackageInfo
            {
                Name = "TestPackage",
                InstalledVersion = "1.0.0",
                Versions = null
            };

            // Act
            var result = packageInfo.GetHighestAvailableVersion();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Returns_null_when_Versions_is_empty()
        {
            // Arrange
            var packageInfo = new ClusterPackageInfo
            {
                Name = "TestPackage",
                InstalledVersion = "1.0.0",
                Versions = []
            };

            // Act
            var result = packageInfo.GetHighestAvailableVersion();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Returns_null_when_InstalledVersion_is_invalid_semver()
        {
            // Arrange
            var packageInfo = new ClusterPackageInfo
            {
                Name = "TestPackage",
                InstalledVersion = "invalid-version",
                Versions = [new ClusterPackageVersion("1.0.0"), new ClusterPackageVersion("2.0.0")]
            };

            // Act
            var result = packageInfo.GetHighestAvailableVersion();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Returns_null_when_InstalledVersion_is_already_highest()
        {
            // Arrange
            var packageInfo = new ClusterPackageInfo
            {
                Name = "TestPackage",
                InstalledVersion = "2.0.0",
                Versions = [new ClusterPackageVersion("1.0.0"), new ClusterPackageVersion("2.0.0")]
            };

            // Act
            var result = packageInfo.GetHighestAvailableVersion();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Returns_highest_version_from_multiple_versions()
        {
            // Arrange
            var packageInfo = new ClusterPackageInfo
            {
                Name = "TestPackage",
                InstalledVersion = "1.0.0",
                Versions = [
                    new ClusterPackageVersion("1.0.0"),
                    new ClusterPackageVersion("2.0.0"),
                    new ClusterPackageVersion("1.5.0"),
                    new ClusterPackageVersion("3.1.0"),
                    new ClusterPackageVersion("2.9.9")]
            };

            // Act
            var result = packageInfo.GetHighestAvailableVersion();

            // Assert
            result.Should().Be("3.1.0");
        }

        [Fact]
        public void Ignores_invalid_versions_in_list()
        {
            // Arrange
            var packageInfo = new ClusterPackageInfo
            {
                Name = "TestPackage",
                InstalledVersion = "1.0.0",
                Versions = [new ClusterPackageVersion("1.0.0"), new ClusterPackageVersion("2.0.0"), new ClusterPackageVersion("not-a-version")]
            };

            // Act
            var result = packageInfo.GetHighestAvailableVersion();

            // Assert
            result.Should().Be("2.0.0");
        }

        [Fact]
        public void Returns_null_when_all_versions_are_invalid()
        {
            // Arrange
            var packageInfo = new ClusterPackageInfo
            {
                Name = "TestPackage",
                InstalledVersion = "1.0.0",
                Versions = [new ClusterPackageVersion("invalid"), new ClusterPackageVersion("not-a-version"), new ClusterPackageVersion("bad-semver")]
            };

            // Act
            var result = packageInfo.GetHighestAvailableVersion();

            // Assert
            result.Should().BeNull();
        }
    }
}
