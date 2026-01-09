namespace ClusterManagement.Client.DataflowEvents.Models;

internal sealed class DataflowRuntimeEvent
{
    internal required Guid EventId { get; init; }
    internal DateTime? Timestamp { get; set; }
    internal string? Value { get; set; }
    internal List<Func<DateTime, string?, Task>> Subscriptions { get; } = [];
}
