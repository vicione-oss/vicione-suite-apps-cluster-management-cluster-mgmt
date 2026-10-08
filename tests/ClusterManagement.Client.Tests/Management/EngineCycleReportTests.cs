using System.Buffers;
using System.Text;
using ClusterManagement.Client.Management.Models;

namespace ClusterManagement.Client.Tests.Management;

public class EngineCycleReportTests
{
    [Fact]
    public void Parses_the_report_the_engine_host_publishes()
    {
        // Arrange
        var deployment = Guid.NewGuid();
        var payload = Payload($$"""{"CycleDuration":4.812,"Deployments":[{"Deployment":"{{deployment}}","Duration":2.1,"Cycle":42}]}""");

        // Act
        var report = EngineCycleReport.TryParse(payload);

        // Assert
        report!.CycleDuration.Should().Be(4.812);
        report.Deployments.Should().ContainSingle().Which.Should().Be(new EngineCycleChainLink(deployment, 2.1));
    }

    [Fact]
    public void Accepts_camel_case_and_skips_unknown_properties()
    {
        // Arrange
        var deployment = Guid.NewGuid();
        var payload = Payload($$"""{"host":{"name":"x"},"cycleDuration":3,"deployments":[{"cycle":7,"deployment":"{{deployment}}","duration":1.5,"extra":[1,2]}]}""");

        // Act
        var report = EngineCycleReport.TryParse(payload);

        // Assert
        report!.CycleDuration.Should().Be(3);
        report.Deployments.Should().ContainSingle().Which.Should().Be(new EngineCycleChainLink(deployment, 1.5));
    }

    [Fact]
    public void Leaves_out_chain_links_without_a_numeric_duration()
    {
        // Arrange
        var kept = Guid.NewGuid();
        var payload = Payload($$"""{"CycleDuration":3,"Deployments":[{"Deployment":"{{Guid.NewGuid()}}","Duration":null},{"Deployment":"{{kept}}","Duration":1.5}]}""");

        // Act
        var report = EngineCycleReport.TryParse(payload);

        // Assert
        report!.Deployments.Should().ContainSingle().Which.Deployment.Should().Be(kept);
    }

    [Fact]
    public void Leaves_out_chain_links_without_a_deployment_id()
    {
        // Arrange
        var payload = Payload("""{"CycleDuration":3,"Deployments":[{"Deployment":"fb-host","Duration":1.5},{"Duration":2}]}""");

        // Act
        var report = EngineCycleReport.TryParse(payload);

        // Assert
        report!.Deployments.Should().BeEmpty();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"CycleDuration":1.0}""")]
    [InlineData("[]")]
    [InlineData("""{"CycleDuration":null,"Deployments":[]}""")]
    [InlineData("""{"CycleDuration":"4.8","Deployments":[]}""")]
    [InlineData("""{"CycleDuration":{"ms":4.8},"Deployments":[]}""")]
    public void Ignores_payloads_that_are_no_cycle_report(string json)
        => EngineCycleReport.TryParse(Payload(json)).Should().BeNull();

    [Fact]
    public void Topic_round_trips_the_engine_host_id()
    {
        // Arrange
        var engineHostId = Guid.NewGuid();

        // Act
        var parsed = EngineCycleReport.TryParseEngineHostId(EngineCycleReport.InfoTopic(engineHostId), out var id);

        // Assert
        parsed.Should().BeTrue();
        id.Should().Be(engineHostId);
    }

    [Theory]
    [InlineData("data/42")]
    [InlineData("not-a-guid/cycle/info")]
    [InlineData("8b6f0a4e-2a4f-4c5e-9b1a-0d7c0e3f1a22/cycle/crash")]
    [InlineData("8b6f0a4e-2a4f-4c5e-9b1a-0d7c0e3f1a22/other/cycle/info")]
    public void Other_topics_are_not_cycle_reports(string topic)
        => EngineCycleReport.TryParseEngineHostId(topic, out _).Should().BeFalse();

    private static ReadOnlySequence<byte> Payload(string json)
        => new(Encoding.UTF8.GetBytes(json));
}
