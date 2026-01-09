namespace ClusterManagement.Backend.Engines;

public sealed class Workflow
{
    public List<ClusterDependency> ClusterDependencies { get; init; } = [];
    public Engine Engine { get; set; } = new();
    public List<Container> Containers { get; init; } = [];
    public List<FunctionBlock> FunctionBlocks { get; init; } = [];
    public List<FunctionBlockConnectorLink> FunctionBlockConnectorLinks { get; init; } = [];
    public List<FunctionBlockConnectorDesign> AdditionalConnectorDesigns { get; init; } = [];
    public List<ExternalConnection> ExternalConnections { get; init; } = [];
    public MqttCommunication PersistenceCommunication { get; set; } = new();
    public MqttCommunication RuntimeFunctionsCommunication { get; set; } = new();
    public CommunicationConfiguration EngineLogCommunication { get; set; } = new();
}
