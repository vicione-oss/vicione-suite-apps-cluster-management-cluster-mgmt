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
    public IAsyncDisposable? Subscription { get; set; }
    public string? Value { get; set; }

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
