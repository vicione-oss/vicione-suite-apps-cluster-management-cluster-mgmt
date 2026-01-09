using System.Text.Json.Serialization;

namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed class IncomingConnector
{
    public FunctionBlockConnector Connector { get; set; } = new();
    public string Channel { get; set; } = string.Empty;
    [JsonConverter(typeof(TypeNameHandlingConverter))]
    public object? Converter { get; set; }
    public string SourceType { get; set; } = string.Empty;
}
