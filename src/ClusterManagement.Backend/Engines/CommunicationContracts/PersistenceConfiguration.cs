namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed class PersistenceConfiguration
{
    public CommunicationConfiguration Communication { get; set; } = new();
    public int IntervalCycles { get; set; }
}
