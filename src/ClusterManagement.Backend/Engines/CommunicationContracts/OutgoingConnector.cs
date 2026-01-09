namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed class OutgoingConnector
{
    public FunctionBlockConnector Connector { get; set; } = new();
    public string Channel { get; set; } = string.Empty;
}
