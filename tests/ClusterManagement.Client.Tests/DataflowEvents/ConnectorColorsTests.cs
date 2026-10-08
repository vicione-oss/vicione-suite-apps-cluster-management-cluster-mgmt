using ClusterManagement.Client.DataflowEvents.Models;

namespace ClusterManagement.Client.Tests.DataflowEvents;

public class ConnectorColorsTests
{
    private const string Bool = "rgb(5, 158, 121)";
    private const string Floating = "rgb(255, 150, 0)";
    private const string Integer = "rgb(190, 236, 121)";
    private const string Text = "rgb(90, 93, 187)";
    private const string Other = "rgb(255, 20, 147)";

    [Theory]
    [InlineData("System.Boolean, System.Private.CoreLib, Version=10.0.0.0", Bool)]
    [InlineData("System.Double, System.Private.CoreLib", Floating)]
    [InlineData("System.Single", Floating)]
    [InlineData("System.Decimal, System.Private.CoreLib", Floating)]
    [InlineData("System.Int32, System.Private.CoreLib", Integer)]
    [InlineData("System.UInt64, System.Private.CoreLib", Integer)]
    [InlineData("System.Byte, System.Private.CoreLib", Integer)]
    [InlineData("System.String, System.Private.CoreLib", Text)]
    [InlineData("System.Char, System.Private.CoreLib", Text)]
    public void Uses_the_editor_color_of_the_value_type(string valueType, string expected)
        => ConnectorColors.For(valueType).Should().Be(expected);

    [Theory]
    [InlineData("ViciOne.Dcw.DcpScanningResult, ViciOne.Dcw")]
    [InlineData("System.Collections.Generic.List`1[[System.Int32, System.Private.CoreLib]], System.Private.CoreLib")]
    [InlineData("")]
    public void Uses_the_default_color_for_other_types(string valueType)
        => ConnectorColors.For(valueType).Should().Be(Other);

    // The editor maps the exact type only, so a nullable value type gets its default color as well.
    [Fact]
    public void Uses_the_default_color_for_a_nullable_value_type()
        => ConnectorColors.For("System.Nullable`1[[System.Boolean, System.Private.CoreLib]], System.Private.CoreLib").Should().Be(Other);
}
