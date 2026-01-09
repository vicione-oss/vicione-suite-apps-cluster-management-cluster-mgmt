namespace ClusterManagement.Backend.Engines;

public sealed class MqttCommunication
{
    public MqttProtocol Protocol { get; set; }
    public string Address { get; set; } = string.Empty;
    public int? TcpPort { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public short QualityOfService { get; set; }
    public bool Retain { get; set; }
}
