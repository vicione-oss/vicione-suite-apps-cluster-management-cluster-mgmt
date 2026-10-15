namespace ClusterManagement.Client.DataflowEvents.Models;

public enum DataflowEventGrouping
{
    None,
    Dataflow,
    Container,
    FunctionBlock,
    Direction,
    ValueType,
}

public enum DataflowEventDirection
{
    All,
    Inputs,
    Outputs,
}

/// <summary>
/// The events of one group, such as the connectors of one function block.
/// </summary>
internal sealed record DataflowEventGroup(string Key, IReadOnlyList<DataflowEventModel> Events)
{
    public int InputCount { get; } = Events.Count(e => e.CanWrite);

    public int OutputCount => Events.Count - InputCount;
}

internal static class DataflowEventGroups
{
    /// <summary>
    /// Groups the events that pass <paramref name="direction"/> and <paramref name="filter"/>; groups and the events in
    /// them follow the order of the dataflows, their containers and function blocks, inputs before outputs.
    /// </summary>
    public static IReadOnlyList<DataflowEventGroup> Build(IEnumerable<DataflowEventModel> events, DataflowEventGrouping grouping,
        DataflowEventDirection direction, string? filter)
    {
        var text = filter?.Trim();
        var shown = events
            .Where(e => Shows(e, direction) && (string.IsNullOrEmpty(text) || Matches(e, text)))
            .OrderBy(e => e.Location, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(e => e.FunctionBlockName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(e => e.FunctionBlockId)
            .ThenBy(e => !e.CanWrite)
            .ThenBy(e => e.ConnectorName, StringComparer.CurrentCultureIgnoreCase);

        return [.. shown
            .GroupBy(e => KeyOf(e, grouping))
            .Select(g => new DataflowEventGroup(g.Key, [.. g]))];
    }

    private static bool Shows(DataflowEventModel dataflowEvent, DataflowEventDirection direction) => direction switch
    {
        DataflowEventDirection.Inputs => dataflowEvent.CanWrite,
        DataflowEventDirection.Outputs => dataflowEvent.CanRead,
        _ => true,
    };

    private static bool Matches(DataflowEventModel dataflowEvent, string text)
        => dataflowEvent.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || dataflowEvent.Location.Contains(text, StringComparison.CurrentCultureIgnoreCase);

    private static string KeyOf(DataflowEventModel dataflowEvent, DataflowEventGrouping grouping) => grouping switch
    {
        DataflowEventGrouping.Dataflow => dataflowEvent.DataflowName,
        DataflowEventGrouping.FunctionBlock => dataflowEvent.FunctionBlockId.ToString(),
        DataflowEventGrouping.Direction => dataflowEvent.CanWrite ? nameof(DataflowEventDirection.Inputs) : nameof(DataflowEventDirection.Outputs),
        DataflowEventGrouping.ValueType => dataflowEvent.ValueType,
        _ => string.Empty,
    };
}
