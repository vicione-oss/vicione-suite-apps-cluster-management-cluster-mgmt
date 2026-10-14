using ClusterManagement.Client.IoddManagement.Extensions;

namespace ClusterManagement.Client.Tests.IoddManagement.Extensions;

public class DeviceInfoExtensionsTests
{
    public sealed class GetDisplayName : DeviceInfoExtensionsTests
    {
        [Theory]
        [InlineData(new[] { "Sensor A" }, "Acme Sensor A")]
        [InlineData(new[] { "Sensor A", "Sensor A" }, "Acme Sensor A")]
        [InlineData(new[] { "Sensor A", "Sensor B" }, "Acme Sensor A…")]
        [InlineData(new[] { "", "Sensor B", "Sensor C" }, "Acme Sensor B…")]
        [InlineData(new string[0], "Acme Sensor")]
        public void Should_name_the_first_variant_and_mark_further_ones(string[] variantNames, string expected)
        {
            // Arrange
            var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(variantNames: variantNames);

            // Act
            var displayName = deviceInfo.GetDisplayName();

            // Assert
            displayName.Should().Be(expected);
        }

        [Fact]
        public void Should_stay_short_for_many_variants()
        {
            // Arrange
            var deviceInfo = DeviceInfoFactory.CreateDeviceInfo(variantNames: [.. Enumerable.Range(1, 50).Select(i => $"PN70{i:00}")]);

            // Act
            var displayName = deviceInfo.GetDisplayName();

            // Assert
            displayName.Should().Be("Acme PN7001…");
        }
    }
}
