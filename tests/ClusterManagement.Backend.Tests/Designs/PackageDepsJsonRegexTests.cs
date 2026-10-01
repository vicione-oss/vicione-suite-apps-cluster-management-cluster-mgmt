using ClusterManagement.Backend.Designs.Consumers;

namespace ClusterManagement.Backend.Tests.Designs;

public sealed class PackageDepsJsonRegexTests
{
    public sealed class Parse
    {
        [Theory]
        [InlineData("    \"ACX.FunctionBlocks.ConstantDouble/0.27.0\": {", "0.27.0")]
        [InlineData("    \"ACX.FunctionBlocks.ConstantDouble/0.27.0-ci234343\": {", "0.27.0-ci234343")]
        [InlineData("    \"ViciOne.Suite.DataPort.Mqtt/2.0.0-beta.1\": {", "2.0.0-beta.1")]
        [InlineData("    \"ViciOne.Suite.DataPort.Mqtt/2.0.0-beta.1-ci2821027829\": {", "2.0.0-beta.1-ci2821027829")]
        [InlineData("    \"ViciOne.Suite.DataPort.Mqtt/1.0.0-rc.2\": {", "1.0.0-rc.2")]
        public void Should_parse_version_from_deps_json_line(string line, string expectedVersion)
        {
            // Arrange + Act
            var result = PackageDepsJsonRegex.Parse(line, out var packageVersion);

            // Assert
            result.Should().BeTrue();
            packageVersion.Should().Be(expectedVersion);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    \"ViciOne.Suite.DataPort.Mqtt\": \"2.0.0\"")] // dependency reference, not a package key
        [InlineData("    \"ViciOne.Suite.DataPort.Mqtt/2.0\": {")] // incomplete version
        [InlineData("    \"ViciOne.Suite.DataPort.Mqtt/2.0.0-\": {")] // empty prerelease
        [InlineData("    \"ViciOne.Suite.DataPort.Mqtt/2.0.0-beta..1\": {")] // empty prerelease identifier
        [InlineData("    \"ViciOne.Suite.DataPort.Mqtt/01.0.0\": {")] // leading zero in version core
        [InlineData("    \"ViciOne.Suite.DataPort.Mqtt/2.0.0-beta.01\": {")] // leading zero in numeric prerelease identifier
        public void Should_return_false_for_invalid_input(string? line)
        {
            // Arrange + Act
            var result = PackageDepsJsonRegex.Parse(line, out var packageVersion);

            // Assert
            result.Should().BeFalse();
            packageVersion.Should().BeNull();
        }
    }

    public sealed class ParseVersion
    {
        [Theory]
        [InlineData("    \"ACX.FunctionBlocks.ConstantDouble/0.27.0\": {")]
        [InlineData("    \"ACX.FunctionBlocks.ConstantDouble/0.27.0-ci234343\": {", true)]
        [InlineData("    \"ViciOne.Suite.DataPort.Mqtt/2.0.0-beta.1\": {", true)]
        [InlineData("    \"ViciOne.Suite.DataPort.Mqtt/2.0.0-beta.1-ci2821027829\": {", true)]
        [InlineData("    \"ViciOne.Suite.DataPort.Mqtt/1.0.0-rc.2\": {", true)]
        public void Should_parse_valid_versions(string line, bool preRelease = false)
        {
            // Arrange + Act
            var parsed = PackageDepsJsonRegex.ParseVersion(line, out var version);

            // Assert
            parsed.Should().BeTrue();
            version.Should().NotBeNull();
            version.IsPrerelease.Should().Be(preRelease);
        }

        [Theory]
        [InlineData("    \"ViciOne.Suite.DataPort.Mqtt/2.0.0-01\": {")] // leading zero in numeric prerelease identifier
        [InlineData("    \"ViciOne.Suite.DataPort.Mqtt/2.0.0-\": {")] // empty prerelease
        public void Should_return_false_for_invalid_versions(string line)
        {
            // Arrange + Act
            var parsed = PackageDepsJsonRegex.ParseVersion(line, out var version);

            // Assert
            parsed.Should().BeFalse();
            version.Should().BeNull();
        }
    }
}
