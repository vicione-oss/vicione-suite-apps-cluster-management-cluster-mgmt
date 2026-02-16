using ClusterManagement.Public.Extensions;

namespace ClusterManagement.Backend.Tests.Extensions;

public class ClusterManagementFormatExtensionsFacts
{
    public class ToClusterManagementFormat
    {
        [Theory]
        [InlineData("Hello World", "\"Hello World\"")]
        [InlineData(23, "23")]
        [InlineData(14.1, "14.1")]
        [InlineData(23.0, "23")]
        [InlineData(false, "false")]
        [MemberData(nameof(GetData))]
        public void Formats_correctly(object? value, string? expectedResult)
            => value.ToClusterManagementFormat().Should().Be(expectedResult);

        public static IEnumerable<object[]> GetData()
            =>
            [
                [new { Value = 23, }, "{\"Value\":23}"],
            ];
    }
}
