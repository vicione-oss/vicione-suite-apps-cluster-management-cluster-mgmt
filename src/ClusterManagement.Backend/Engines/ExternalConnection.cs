namespace ClusterManagement.Backend.Engines;

public sealed class ExternalConnection
{
    public string? Name { get; set; }
    public CommunicationConfiguration Communication { get; set; } = new();
    public List<IncomingLink> IncomingLinks { get; init; } = [];
    public List<OutgoingLink> OutgoingLinks { get; init; } = [];
}
