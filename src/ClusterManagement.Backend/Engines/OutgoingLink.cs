namespace ClusterManagement.Backend.Engines;

public sealed class OutgoingLink
{
    public Guid ConnectorId { get; set; }
    public Guid RemoteId { get; set; }
}
