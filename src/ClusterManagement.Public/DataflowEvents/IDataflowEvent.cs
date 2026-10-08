namespace ClusterManagement.Public.DataflowEvents;

public interface IDataflowEvent
{
    /// <summary>
    /// Indicates, if the value of the Event can be read by the user
    /// </summary>
    bool CanRead { get; }

    /// <summary>
    /// Indicates, if the value of the Event can be set by the user
    /// </summary>
    bool CanWrite { get; }

    /// <summary>
    /// ID of the dataflow object responsible emitting or receiving the Event
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Display name of the event, <see cref="FunctionBlockName"/> and <see cref="ConnectorName"/> joined by a dot
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The dotnet type of the value
    /// </summary>
    string ValueType { get; }

    /// <summary>
    /// ID of the function block the connector belongs to
    /// </summary>
    Guid FunctionBlockId { get; }

    /// <summary>
    /// Name of the function block the connector belongs to
    /// </summary>
    string FunctionBlockName { get; }

    /// <summary>
    /// Name of the connector, without the name of its function block
    /// </summary>
    string ConnectorName { get; }

    /// <summary>
    /// Name of the dataflow the function block is part of
    /// </summary>
    string DataflowName { get; }

    /// <summary>
    /// Names of the containers between the dataflow and the function block, outermost first; empty when the function
    /// block sits directly in the dataflow
    /// </summary>
    IReadOnlyList<string> ContainerPath { get; }
}
