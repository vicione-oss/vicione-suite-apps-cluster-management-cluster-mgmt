using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Engines;

public sealed class FunctionBlock
{
    public Guid Id { get; set; }
    public Guid? ContainerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid DesignId { get; set; }
    public FunctionBlockRunMode RunMode { get; set; }
    public uint CycleFrequency { get; set; }
    public uint RunIndex { get; set; }
    public List<FunctionBlockConnector> Connectors { get; init; } = [];
    public List<FunctionBlockSetting> Settings { get; init; } = [];
    public List<FunctionBlockVariable> Variables { get; init; } = [];
}
