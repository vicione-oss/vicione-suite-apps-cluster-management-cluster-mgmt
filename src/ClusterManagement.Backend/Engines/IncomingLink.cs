namespace ClusterManagement.Backend.Engines;

public sealed class IncomingLink
{
    public Guid ConnectorId { get; set; }
    public Guid RemoteId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public TypedObject? TypeConverter { get; set; }
}
