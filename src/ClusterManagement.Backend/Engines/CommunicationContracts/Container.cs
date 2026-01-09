namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed class Container
{
    public string UniqueIdentifier { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<FunctionBlock> FunctionBlocks { get; init; } = [];
}
