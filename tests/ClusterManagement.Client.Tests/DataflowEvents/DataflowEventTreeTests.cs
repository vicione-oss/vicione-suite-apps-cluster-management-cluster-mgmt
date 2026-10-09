using System.Collections.Concurrent;
using ClusterManagement.Client.DataflowEvents.Models;

namespace ClusterManagement.Client.Tests.DataflowEvents;

public class DataflowEventTreeTests
{
    // The events of one function block share its id, as they do in a cluster.
    private static readonly ConcurrentDictionary<string, Guid> s_functionBlockIds = new();

    private readonly DataflowEventModel _weight = Event("Line 3", "Scale", "Weight", canWrite: false);
    private readonly DataflowEventModel _threshold = Event("Line 3", "Crest", "Threshold", canWrite: true, "Pump 1");
    private readonly DataflowEventModel _crestValue = Event("Line 3", "Crest", "Value", canWrite: false, "Pump 1");
    private readonly DataflowEventModel _speed = Event("Line 3", "Axis", "Speed", canWrite: false, "Pump 1", "Motor");
    private readonly DataflowEventModel _otherLine = Event("Line 4", "Valve", "Open", canWrite: true, "Pump 1");

    [Fact]
    public void Nests_the_containers_under_their_dataflow_level_by_level()
    {
        // Act
        var tree = DataflowEventTree.Build([_speed, _otherLine, _crestValue, _weight, _threshold], DataflowEventDirection.All, null);

        // Assert
        tree.Select(n => n.Name).Should().Equal("Line 3", "Line 4");
        var pump = tree[0].Children.Should().ContainSingle().Subject;
        pump.Name.Should().Be("Pump 1");
        pump.Path.Should().Equal("Line 3", "Pump 1");
        pump.Children.Should().ContainSingle().Which.Path.Should().Equal("Line 3", "Pump 1", "Motor");
    }

    [Fact]
    public void Orders_dataflows_and_containers_by_name_and_inputs_before_outputs()
    {
        // Arrange
        var pumpB = Event("Line 3", "Axis", "Unit", canWrite: false, "b Pump");
        var pumpA = Event("Line 3", "Axis", "Unit", canWrite: false, "A Pump");
        var lineA = Event("a Line", "Axis", "Unit", canWrite: false);

        // Act
        var tree = DataflowEventTree.Build([pumpB, _crestValue, lineA, pumpA, _threshold], DataflowEventDirection.All, null);

        // Assert
        tree.Select(n => n.Name).Should().Equal("a Line", "Line 3");
        tree[1].Children.Select(n => n.Name).Should().Equal("A Pump", "b Pump", "Pump 1");
        tree[1].Children[2].Events.Should().Equal(_threshold, _crestValue);
    }

    [Fact]
    public void Counts_the_inputs_and_outputs_of_everything_below_a_node()
    {
        // Act
        var tree = DataflowEventTree.Build([_weight, _threshold, _crestValue, _speed], DataflowEventDirection.All, null);

        // Assert
        tree[0].Group.InputCount.Should().Be(1);
        tree[0].Group.OutputCount.Should().Be(3);
    }

    [Fact]
    public void Holds_the_events_of_a_container_before_those_of_its_sub_containers()
    {
        // Act
        var tree = DataflowEventTree.Build([_speed, _crestValue, _weight], DataflowEventDirection.All, null);

        // Assert
        tree[0].Events.Should().Equal(_weight);
        tree[0].Children[0].Events.Should().Equal(_crestValue);
        tree[0].Group.Events.Should().Equal(_weight, _crestValue, _speed);
    }

    [Fact]
    public void Keeps_containers_of_the_same_name_in_different_dataflows_apart()
    {
        // Act
        var tree = DataflowEventTree.Build([_crestValue, _otherLine], DataflowEventDirection.All, null);

        // Assert
        var pumps = tree.Select(n => n.Children.Single()).ToArray();
        pumps.Select(p => p.Events.Single()).Should().Equal(_crestValue, _otherLine);
        pumps[0].Group.Key.Should().NotBe(pumps[1].Group.Key);
    }

    [Fact]
    public void Gives_every_node_a_key_of_its_own_even_when_names_look_like_a_path()
    {
        // Arrange
        var nested = Event("Line", "Axis", "Unit", canWrite: false, "3");
        var flat = Event("Line › 3", "Axis", "Unit", canWrite: false);

        // Act
        var tree = DataflowEventTree.Build([nested, flat], DataflowEventDirection.All, null);

        // Assert
        tree.Concat(tree.SelectMany(n => n.Children)).Select(n => n.Group.Key).Should().OnlyHaveUniqueItems().And.HaveCount(3);
    }

    // The events know their containers by name only, so this cannot tell two such containers apart.
    [Fact]
    public void Merges_sibling_containers_of_the_same_name()
    {
        // Arrange
        var otherPump = Event("Line 3", "Valve", "Open", canWrite: true, "Pump 1");

        // Act
        var tree = DataflowEventTree.Build([_crestValue, otherPump], DataflowEventDirection.All, null);

        // Assert
        tree[0].Children.Should().ContainSingle().Which.Events.Should().BeEquivalentTo([_crestValue, otherPump]);
    }

    [Fact]
    public void A_filter_keeps_the_branches_with_a_match_and_their_ancestors()
    {
        // Act
        var tree = DataflowEventTree.Build([_weight, _threshold, _crestValue, _speed, _otherLine], DataflowEventDirection.All, "speed");

        // Assert
        var line = tree.Should().ContainSingle().Subject;
        line.Events.Should().BeEmpty();
        var pump = line.Children.Should().ContainSingle().Subject;
        pump.Events.Should().BeEmpty();
        pump.Children.Should().ContainSingle().Which.Events.Should().Equal(_speed);
    }

    [Fact]
    public void Leaves_out_the_branches_without_an_event_of_the_direction()
    {
        // Act
        var tree = DataflowEventTree.Build([_weight, _threshold, _otherLine], DataflowEventDirection.Outputs, null);

        // Assert
        tree.Should().ContainSingle().Which.Children.Should().BeEmpty();
    }

    private static DataflowEventModel Event(string dataflow, string functionBlockName, string connectorName, bool canWrite, params string[] containers)
        => new()
        {
            EventId = Guid.NewGuid(),
            Name = $"{functionBlockName}.{connectorName}",
            ValueType = "System.Double, System.Private.CoreLib",
            CanWrite = canWrite,
            CanRead = !canWrite,
            FunctionBlockId = s_functionBlockIds.GetOrAdd(string.Join("/", [dataflow, .. containers, functionBlockName]), _ => Guid.NewGuid()),
            FunctionBlockName = functionBlockName,
            ConnectorName = connectorName,
            DataflowName = dataflow,
            ContainerPath = containers,
        };
}
