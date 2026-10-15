using ClusterManagement.Client.Management.Models;
using ClusterManagement.Internal.Requests;

namespace ClusterManagement.Client.Tests.Management;

public class EngineRuntimeModelTests
{
    private static readonly EngineRuntimeModel s_engine = new(Guid.NewGuid(), Guid.NewGuid(), "Filling", 100) { IsChecked = true, IsReachable = true };

    [Theory]
    [InlineData(EngineState.Stopped, true)]
    [InlineData(EngineState.StartFailed, true)]
    [InlineData(EngineState.Running, false)]
    [InlineData(EngineState.Starting, false)]
    [InlineData(EngineState.Stopping, false)]
    public void Is_stopped_when_it_neither_runs_nor_changes_its_state(EngineState state, bool expected)
        => (s_engine with { State = state }).IsStopped.Should().Be(expected);

    [Fact]
    public void Is_not_stopped_while_its_host_does_not_answer()
        => (s_engine with { State = EngineState.Stopped, IsReachable = false }).IsStopped.Should().BeFalse();
}
