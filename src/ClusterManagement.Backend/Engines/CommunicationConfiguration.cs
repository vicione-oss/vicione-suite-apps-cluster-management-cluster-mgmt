namespace ClusterManagement.Backend.Engines;

public sealed record class CommunicationConfiguration
{
    public string Id { get; set; } = string.Empty;
    public List<CommunicationProperty> Properties { get; init; } = [];
}
