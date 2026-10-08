using ClusterManagement.Client.DataflowEvents.Models;

namespace ClusterManagement.Client.Tests.DataflowEvents;

public class ValueTypeNamesTests
{
    [Theory]
    [InlineData("System.Boolean, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e", "Boolean")]
    [InlineData("System.Collections.Generic.List`1[[System.Int32, System.Private.CoreLib]], System.Private.CoreLib", "List<Int32>")]
    [InlineData("System.Double[], System.Private.CoreLib", "Double[]")]
    [InlineData("ViciOne.DeviceTree.Contracts.Scanning.DcpScanningResult, ViciOne.DeviceTree.Contracts, Version=2.1.0.0, Culture=neutral, PublicKeyToken=null", "DcpScanningResult")]
    [InlineData("Vendor.Unknown.Generic`2[[System.Int32, System.Private.CoreLib],[System.String, System.Private.CoreLib]], Vendor", "Generic<Int32, String>")]
    [InlineData("Vendor.Outer+Nested, Vendor", "Nested")]
    [InlineData("Int32", "Int32")]
    public void Short_name_drops_namespace_assembly_and_version(string assemblyQualifiedName, string expected)
        => ValueTypeNames.Short(assemblyQualifiedName).Should().Be(expected);

    [Theory]
    [InlineData("System.Boolean, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e", "System.Boolean")]
    [InlineData("System.Collections.Generic.List`1[[System.Int32, System.Private.CoreLib]], System.Private.CoreLib", "System.Collections.Generic.List`1[[System.Int32, System.Private.CoreLib]]")]
    [InlineData("System.Int32", "System.Int32")]
    public void Full_name_drops_only_the_assembly(string assemblyQualifiedName, string expected)
        => ValueTypeNames.FullName(assemblyQualifiedName).Should().Be(expected);

    [Theory]
    [InlineData("System.Boolean, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e", true)]
    [InlineData("System.Boolean, mscorlib, Version=4.0.0.0", true)]
    [InlineData("Vendor.Boolean, Vendor", false)]
    [InlineData("System.Collections.Generic.List`1[[System.Boolean, System.Private.CoreLib]], System.Private.CoreLib", false)]
    public void Is_compares_the_full_name_of_the_type(string assemblyQualifiedName, bool expected)
        => ValueTypeNames.Is(assemblyQualifiedName, typeof(bool)).Should().Be(expected);
}
