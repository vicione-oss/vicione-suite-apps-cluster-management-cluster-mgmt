using System.Globalization;
using ClusterManagement.Client.Management.Extensions;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Internal.Requests;

namespace ClusterManagement.Client.Tests.Management;

public class EngineRuntimeModelExtensionsTests
{
    private static readonly EngineRuntimeModel s_engine = new(Guid.NewGuid(), Guid.NewGuid(), "Filling", 100) { IsChecked = true, IsReachable = true };

    [Theory]
    [InlineData(EngineState.Running, "running")]
    [InlineData(EngineState.Stopped, "previous")]
    [InlineData(EngineState.Starting, "deploying")]
    [InlineData(EngineState.Stopping, "deploying")]
    [InlineData(EngineState.StartFailed, "failed")]
    public void Status_name_follows_the_state(EngineState state, string expected)
        => (s_engine with { State = state }).StatusName().Should().Be(expected);

    [Fact]
    public void Texts_name_each_state()
        => Enum.GetValues<EngineState>().Select(state => (s_engine with { State = state }).StateText()).Should().Equal(
            ClusterOverview.EngineStopped, ClusterOverview.EngineRunning, ClusterOverview.EngineStarting, ClusterOverview.EngineStopping,
            ClusterOverview.EngineStartFailed);

    [Fact]
    public void An_engine_whose_host_does_not_answer_reads_as_not_reachable()
    {
        var engine = s_engine with { State = EngineState.Running, IsReachable = false };

        engine.StatusName().Should().Be("failed");
        engine.StateText().Should().Be(ClusterOverview.EngineNotReachable);
    }

    [Fact]
    public void An_engine_its_instance_has_not_deployed_reads_as_not_deployed()
    {
        var engine = s_engine with { IsDeployed = false };

        engine.StatusName().Should().Be("previous");
        engine.StateText().Should().Be(ClusterOverview.EngineNotDeployed);
    }

    [Fact]
    public void A_deployment_shows_every_engine_as_deploying()
    {
        var engine = s_engine with { State = EngineState.StartFailed };

        engine.StatusName(deploying: true).Should().Be("deploying");
        engine.StateText(deploying: true).Should().Be(ClusterOverview.EngineDeploying);
    }

    [Fact]
    public void An_engine_not_asked_yet_shows_no_state()
    {
        var engine = s_engine with { IsChecked = false };

        engine.StatusName(deploying: false).Should().Be("previous");
        engine.StateText(deploying: false).Should().Be("…");
    }

    [Fact]
    public void Running_text_counts_the_engines_that_run()
        => new[] { s_engine with { State = EngineState.Running }, s_engine with { State = EngineState.Stopped } }
            .RunningText(deploying: false).Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.EnginesRunning, 1, 2));

    [Fact]
    public void Running_text_says_that_a_deployment_runs()
        => new[] { s_engine with { State = EngineState.Running }, s_engine with { IsChecked = false } }
            .RunningText(deploying: true).Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.EnginesDeploying, 2, 2));

    [Fact]
    public void Running_text_is_null_before_every_engine_has_answered()
        => new[] { s_engine with { State = EngineState.Running }, s_engine with { IsChecked = false } }
            .RunningText(deploying: false).Should().BeNull();
}
