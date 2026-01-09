namespace ClusterManagement.Backend.Engines;

public sealed class FunctionBlockConnectorLink
{
    public Guid Id { get; set; }
    public Guid SourceConnectorId { get; set; }
    public Guid DestinationConnectorId { get; set; }
    public TypedObject? TypeConverter { get; set; }
}
