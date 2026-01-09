namespace ClusterManagement.Backend.Engines;

public sealed class FunctionBlockConnectorDesign
{
    public Guid ConnectorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Type ValueType { get; set; } = typeof(void);
    public bool IsInput { get; set; }
}
