namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed class ExternalConnection
{
    public string UniqueIdentifier { get; set; } = string.Empty;
    public string? Name { get; set; }
    public CommunicationConfiguration Communication { get; set; } = new();
    public List<IncomingConnector> IncomingConnectors { get; init; } = [];
    public List<OutgoingConnector> OutgoingConnectors { get; init; } = [];
}
