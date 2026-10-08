using ClusterManagement.Client.DataflowEvents.Models;

namespace ClusterManagement.Client.Tests.DataflowEvents;

public class DataflowEventGroupsTests
{
    private static readonly Guid s_crest = Guid.NewGuid();
    private static readonly Guid s_axis = Guid.NewGuid();

    private readonly DataflowEventModel _threshold = Event(s_crest, "Crest", "Threshold", canWrite: true, "System.Double", "Pump 1");
    private readonly DataflowEventModel _crestValue = Event(s_crest, "Crest", "Value", canWrite: false, "System.Double", "Pump 1");
    private readonly DataflowEventModel _axisUnit = Event(s_axis, "a Axis", "Unit", canWrite: false, "System.String", "Pump 2");

    [Fact]
    public void Groups_by_function_block_in_the_order_of_their_location_with_inputs_first()
    {
        // Act
        var groups = DataflowEventGroups.Build([_axisUnit, _crestValue, _threshold], DataflowEventGrouping.FunctionBlock, DataflowEventDirection.All, null);

        // Assert
        groups.Select(g => g.Events[0].FunctionBlockName).Should().Equal("Crest", "a Axis");
        groups[0].Events.Should().Equal(_threshold, _crestValue);
        groups[0].InputCount.Should().Be(1);
        groups[0].OutputCount.Should().Be(1);
    }

    [Fact]
    public void Function_blocks_with_the_same_name_stay_apart()
    {
        // Arrange
        var otherCrest = Event(Guid.NewGuid(), "Crest", "Value", canWrite: false, "System.Double", "Pump 2");

        // Act
        var groups = DataflowEventGroups.Build([_crestValue, otherCrest], DataflowEventGrouping.FunctionBlock, DataflowEventDirection.All, null);

        // Assert
        groups.Should().HaveCount(2);
    }

    [Fact]
    public void Groups_by_container()
    {
        // Act
        var groups = DataflowEventGroups.Build([_threshold, _axisUnit], DataflowEventGrouping.Container, DataflowEventDirection.All, null);

        // Assert
        groups.Select(g => g.Events[0].Location).Should().Equal("Line 3 › Pump 1", "Line 3 › Pump 2");
    }

    [Fact]
    public void Groups_by_direction_into_inputs_and_outputs()
    {
        // Act
        var groups = DataflowEventGroups.Build([_crestValue, _threshold, _axisUnit], DataflowEventGrouping.Direction, DataflowEventDirection.All, null);

        // Assert
        groups.Select(g => g.Events.Count).Should().Equal(1, 2);
        groups[1].Events.Should().OnlyContain(e => e.CanRead);
    }

    [Fact]
    public void Groups_by_value_type()
    {
        // Act
        var groups = DataflowEventGroups.Build([_crestValue, _axisUnit], DataflowEventGrouping.ValueType, DataflowEventDirection.All, null);

        // Assert
        groups.Select(g => g.Events[0].ConnectorName).Should().Equal("Value", "Unit");
    }

    [Fact]
    public void Without_grouping_every_event_is_in_one_group()
    {
        // Act
        var groups = DataflowEventGroups.Build([_crestValue, _threshold, _axisUnit], DataflowEventGrouping.None, DataflowEventDirection.All, null);

        // Assert
        groups.Should().ContainSingle().Which.Events.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(nameof(DataflowEventDirection.Inputs), 1)]
    [InlineData(nameof(DataflowEventDirection.Outputs), 2)]
    [InlineData(nameof(DataflowEventDirection.All), 3)]
    public void Shows_the_events_of_a_direction(string direction, int expected)
        => DataflowEventGroups.Build([_crestValue, _threshold, _axisUnit], DataflowEventGrouping.None, Enum.Parse<DataflowEventDirection>(direction), null)
            .Sum(g => g.Events.Count).Should().Be(expected);

    [Theory]
    [InlineData(" threshold ")]
    [InlineData("pump 1")]
    public void Filter_matches_the_name_and_the_location(string filter)
        => DataflowEventGroups.Build([_crestValue, _threshold, _axisUnit], DataflowEventGrouping.None, DataflowEventDirection.All, filter)
            .SelectMany(g => g.Events).Should().Contain(_threshold).And.NotContain(_axisUnit);

    private static DataflowEventModel Event(Guid functionBlockId, string functionBlockName, string connectorName, bool canWrite, string valueType,
        string container)
        => new()
        {
            EventId = Guid.NewGuid(),
            Name = $"{functionBlockName}.{connectorName}",
            ValueType = $"{valueType}, System.Private.CoreLib",
            CanWrite = canWrite,
            CanRead = !canWrite,
            FunctionBlockId = functionBlockId,
            FunctionBlockName = functionBlockName,
            ConnectorName = connectorName,
            DataflowName = "Line 3",
            ContainerPath = [container],
        };
}
