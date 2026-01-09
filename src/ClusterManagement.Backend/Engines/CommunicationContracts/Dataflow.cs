namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed class Dataflow
{
    public List<FunctionBlock> FunctionBlocks { get; init; } = [];
    public List<FunctionBlockConnectorDesign> AdditionalConnectorDesigns { get; init; } = [];
    public List<ConnectorLink> Links { get; init; } = [];
    public List<Container> Containers { get; init; } = [];
    public List<ContainerAssignment> ContainerHierarchy { get; init; } = [];
}
