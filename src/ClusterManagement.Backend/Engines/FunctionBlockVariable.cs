namespace ClusterManagement.Backend.Engines;

public sealed class FunctionBlockVariable
{
    public Guid Id { get; set; }
    public Guid DesignId { get; set; }
    public TypedObject? Value { get; set; }
    public DateTime Timestamp { get; set; }
}
