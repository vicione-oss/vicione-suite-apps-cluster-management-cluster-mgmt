using ClusterManagement.Client.DataflowEvents.Models;

namespace ClusterManagement.Client.Tests.DataflowEvents;

public class DataflowEventModel_ReadableValue
{
    [Theory]
    [InlineData("\"\\u00B0C\"", "\"°C\"")]
    [InlineData("{\"Unit\":\"\\u00B0C\",\"Value\":35}", "{\"Unit\":\"°C\",\"Value\":35}")]
    [InlineData("\"Line \\u0022A\\u0022\"", "\"Line \\\"A\\\"\"")]
    public void Writes_escaped_characters_as_themselves(string value, string expected)
        => Model(value).ReadableValue.Should().Be(expected);

    [Theory]
    [InlineData("\"mA\"")]
    [InlineData("35")]
    [InlineData("not json \\u00")]
    public void Keeps_a_value_without_escapes_or_that_is_no_json(string value)
        => Model(value).ReadableValue.Should().Be(value);

    [Fact]
    public void Shows_the_readable_value()
        => Model("\"\\u00B0C\"").DisplayValue.Should().Be("\"°C\"");

    private static DataflowEventModel Model(string value)
        => new() { EventId = Guid.NewGuid(), Name = "Diagnostics.TemperatureUnit", ValueType = "System.String", Value = value };
}
