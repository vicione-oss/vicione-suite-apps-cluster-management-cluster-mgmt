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
        [InlineData("2.0.0-beta.1-linux-x64_1.0.0.json", "2.0.0-beta.1", "linux-x64", "1.0.0")]
        [InlineData("2.0.0-beta.1-ci2821027829-win-x64_1.0.0.json", "2.0.0-beta.1-ci2821027829", "win-x64", "1.0.0")]
        [InlineData("1.0.0-rc.2-linux-arm64_1.0.0.json", "1.0.0-rc.2", "linux-arm64", "1.0.0")]
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
        [InlineData("1.0.0-osx-arm64_2.0.0.json")] // unknown runtime
        [InlineData("1.0.0-nil-x64_2.0.0.json")] // unknown runtime built from the letters of a known one
        [InlineData("2.0.0-beta.1-linux-x64.json")] // missing core version part
        [InlineData("1.0.0-linux-x64_2.0.0.jsonx")]
        [InlineData("1.0.0-linux-x64_2.0.0_json")]
        [InlineData("01.0.0-linux-x64_2.0.0.json")] // leading zero in version core
        [InlineData("2.0.0-beta.01-linux-x64_2.0.0.json")] // leading zero in numeric prerelease identifier
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
        [InlineData("2.0.0-beta.1-linux-x64_1.0.0.json", true)]
        [InlineData("2.0.0-beta.1-ci2821027829-win-x64_1.0.0.json", true)]
        [InlineData("1.0.0-rc.2-linux-arm64_1.0.0.json", true)]
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

