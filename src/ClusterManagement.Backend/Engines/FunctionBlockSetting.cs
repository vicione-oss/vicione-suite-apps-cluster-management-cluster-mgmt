namespace ClusterManagement.Backend.Engines;

public sealed class FunctionBlockSetting
{
    public Guid Id { get; set; }
    public Guid DesignId { get; set; }
    public TypedObject? Value { get; set; }
    public DateTime Timestamp { get; set; }
}
