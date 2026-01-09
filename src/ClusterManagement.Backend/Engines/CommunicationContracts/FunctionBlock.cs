namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed class FunctionBlock
{
    public string DesignIdentifier { get; set; } = string.Empty;
    public string UniqueIdentifier { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public FunctionBlockRunMode RunMode { get; set; }
    public uint CycleFrequency { get; set; }
    public List<FunctionBlockSetting> Settings { get; init; } = [];
    public List<FunctionBlockVariable> Variables { get; init; } = [];
    public List<FunctionBlockConnector> Connectors { get; init; } = [];
}
