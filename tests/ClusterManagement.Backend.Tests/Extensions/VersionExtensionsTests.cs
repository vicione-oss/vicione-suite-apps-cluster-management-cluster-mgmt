using AwesomeAssertions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Public.Enums;
using Xunit;

namespace ClusterManagement.Backend.Tests.Extensions;

public class VersionExtensionsTests
{
    public sealed class GetVersionIncrement
    {
        [Fact]
        public void Should_increment_major_version_and_reset_others()
        {
            // Arrange
            var version = new Version(1, 2, 3);

            // Act
            var result = version.GetVersionIncrement(VersionIncrementKind.Major);

            // Assert
            result.Should().Be(new Version(2, 0, 0));
        }

        [Fact]
        public void Should_increment_minor_version_and_reset_patch()
        {
            // Arrange
            var version = new Version(1, 2, 3);

            // Act
            var result = version.GetVersionIncrement(VersionIncrementKind.Minor);

            // Assert
            result.Should().Be(new Version(1, 3, 0));
        }

        [Fact]
        public void Should_increment_patch_version()
        {
            // Arrange
            var version = new Version(1, 2, 3);

            // Act
            var result = version.GetVersionIncrement(VersionIncrementKind.Patch);

            // Assert
            result.Should().Be(new Version(1, 2, 4));
        }

        [Fact]
        public void Should_throw_for_invalid_update_kind()
        {
            // Arrange
            var version = new Version(1, 2, 3);
            var invalidUpdateType = (VersionIncrementKind)99;

            // Act
            var act = () => version.GetVersionIncrement(invalidUpdateType);

            // Assert
            act.Should().Throw<InvalidOperationException>()
               .WithMessage("99 is no valid VersionIncrementKind");
        }
    }
}

