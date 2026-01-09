namespace ClusterManagement.Backend.EngineHosts;

public sealed record EngineHostAppSettings
{
    public Guid Id { get; set; }
    public MqttSettings CommandBus { get; set; } = new();
    public string DeploymentsDirectory { get; set; } = string.Empty;
    public string PackagesDirectory { get; set; } = string.Empty;
    public SerilogSettings Serilog { get; set; } = new();
}
