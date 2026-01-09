using AwesomeAssertions;
using Xunit;

namespace ClusterManagement.Backend.Tests;

public sealed class MetadataArtifactNameRegexTests
{
    public sealed class Parse
    {
        [Theory]
        [InlineData("0.28.0-linux-x64_0.48.0.json", "0.28.0", "linux-x64", "0.48.0")]
        [InlineData("0.28.0-ci234343-linux-x64_0.48.0.json", "0.28.0-ci234343", "linux-x64", "0.48.0")]
        [InlineData("1.2.3-win-arm64_2.0.0.json", "1.2.3", "win-arm64", "2.0.0")]
        [InlineData("10.20.30-linux-x64_11.22.33.json", "10.20.30", "linux-x64", "11.22.33")]
        public void Should_parse_valid_filename_correctly(string input, string expectedVersion, string expectedRuntime, string expectedCore)
        {
            // Arrange + Act
            var result = MetadataArtifactNameRegex.Parse(input, out var packageVersion, out var runtime, out var coreVersion);

            // Assert
            result.Should().BeTrue();
            packageVersion.Should().Be(expectedVersion);
            runtime.Should().Be(expectedRuntime);
            coreVersion.Should().Be(expectedCore);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("invalid.json")]
        [InlineData("1.0.0-linux-x64.json")] // missing core version part
        [InlineData("1.0.0_linux-x64_2.0.0.json")] // wrong separator
        [InlineData("1.0.0-linux_x64_2.0.0.json")] // similar but different
        public void Should_return_false_for_invalid_input(string? input)
        {
            // Arrange + Act
            var result = MetadataArtifactNameRegex.Parse(input, out var packageVersion, out var runtime, out var coreVersion);

            // Assert
            result.Should().BeFalse();
            packageVersion.Should().BeNull();
            runtime.Should().BeNull();
            coreVersion.Should().BeNull();
        }
    }

    public class ParseVersion
    {
        [Theory]
        [InlineData("0.28.0-linux-x64_0.48.0.json")]
        [InlineData("0.28.0-ci234343-linux-x64_0.48.0.json", true)]
        [InlineData("1.2.3-win-arm64_2.0.0.json")]
        [InlineData("10.20.30-linux-x64_11.22.33.json")]
        public void Should_parse_valid_versions(string input, bool preRelease = false)
        {
            // Arrange + Act
            var parsed = MetadataArtifactNameRegex.ParseVersion(input, out var version);

            // Assert
            parsed.Should().BeTrue();
            version.Should().NotBeNull();
            version.IsPrerelease.Should().Be(preRelease);
        }
    }
}

