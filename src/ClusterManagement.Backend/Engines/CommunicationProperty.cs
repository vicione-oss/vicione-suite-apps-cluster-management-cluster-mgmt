namespace ClusterManagement.Backend.Engines;

public sealed record class CommunicationProperty(string Id)
{
    public TypedObject? Value { get; set; }
}
