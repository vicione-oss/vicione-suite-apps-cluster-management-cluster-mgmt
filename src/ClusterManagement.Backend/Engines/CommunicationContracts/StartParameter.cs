namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed class StartParameter : IStartParameter
{
    public Dataflow Dataflow { get; set; } = new();
    public DataDistribution DataDistribution { get; set; } = new();
    public EngineLogConfiguration EngineLog { get; set; } = new();
    public Engine Engine { get; set; } = new();
}
