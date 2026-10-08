namespace ClusterManagement.Internal.Tests;

public class EngineHostUnitsTests
{
    private static readonly Guid s_engineHostId = Guid.Parse("6f1c2a52-9a3e-4a5f-8d3b-2a6c1f0e7b91");

    [Theory]
    [InlineData(false, "enginehost@6f1c2a52-9a3e-4a5f-8d3b-2a6c1f0e7b91.service")]
    [InlineData(true, "enginehost-privileged@6f1c2a52-9a3e-4a5f-8d3b-2a6c1f0e7b91.service")]
    public void Of_names_the_unit_the_engine_host_runs_in(bool elevatedPrivileges, string expected)
        => EngineHostUnits.Of(s_engineHostId, elevatedPrivileges).Should().Be(expected);

    [Fact]
    public void Pattern_matches_the_unit_with_and_without_elevated_privileges()
        => EngineHostUnits.Pattern(s_engineHostId).Should().Be("enginehost*@6f1c2a52-9a3e-4a5f-8d3b-2a6c1f0e7b91.service");

    [Fact]
    public void Reset_names_the_reset_unit_of_the_engine_host()
        => EngineHostUnits.Reset(s_engineHostId).Should().Be("enginehost-reset@6f1c2a52-9a3e-4a5f-8d3b-2a6c1f0e7b91.service");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsPrivileged_tells_the_units_apart(bool elevatedPrivileges)
        => EngineHostUnits.IsPrivileged(EngineHostUnits.Of(s_engineHostId, elevatedPrivileges)).Should().Be(elevatedPrivileges);
}
