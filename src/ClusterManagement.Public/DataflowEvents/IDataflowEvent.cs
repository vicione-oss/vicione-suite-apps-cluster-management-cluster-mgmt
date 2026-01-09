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
    /// Name of the dataflow object responsible emitting or receiving the Event
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The dotnet type of the value
    /// </summary>
    string ValueType { get; }
}
