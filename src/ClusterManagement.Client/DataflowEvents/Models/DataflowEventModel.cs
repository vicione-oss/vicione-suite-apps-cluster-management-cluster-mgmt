using ClusterManagement.Public.DataflowEvents;

namespace ClusterManagement.Client.DataflowEvents.Models;

internal class DataflowEventModel : IDataflowEvent
{
    public const int MaxDisplayLength = 150;

    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public required Guid EventId { get; init; }
    public required string Name { get; set; }
    public string? Unit { get; set; }
    public required string ValueType { get; init; }
    public Guid FunctionBlockId { get; init; }
    public string FunctionBlockName { get; init; } = string.Empty;
    public string ConnectorName { get; init; } = string.Empty;
    public string DataflowName { get; init; } = string.Empty;
    public IReadOnlyList<string> ContainerPath { get; init; } = [];
    public IAsyncDisposable? Subscription { get; set; }
    public string? Value { get; set; }

    /// <summary>
    /// The value an input row shows and writes: what was typed, or for a Boolean input the value its checkbox wrote
    /// last. It stays after a write, so the same value can be written again.
    /// </summary>
    public string? PendingValue { get; set; }

    /// <summary>
    /// The dataflow and the containers the function block sits in, such as <c>Line 3 › Pump 1</c>.
    /// </summary>
    public string Location => string.Join(" › ", [DataflowName, .. ContainerPath]);

    public string? DisplayValue
    {
        get
        {
            if (string.IsNullOrEmpty(Value))
                return null;

            if (Value.Length <= MaxDisplayLength)
                return Value;

            return string.Concat(Value.AsSpan(0, MaxDisplayLength), "...");
        }
    }
    public DateTime? Timestamp { get; set; }
}
