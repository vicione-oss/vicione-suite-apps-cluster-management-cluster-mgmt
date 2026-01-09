namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed class FunctionBlockConnectorDesign
{
    public FunctionBlockConnector Connector { get; set; } = new();
    public string Name { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    public bool IsInput { get; set; }
}
