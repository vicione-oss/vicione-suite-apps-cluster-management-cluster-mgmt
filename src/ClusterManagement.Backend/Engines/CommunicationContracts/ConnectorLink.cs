using System.Text.Json.Serialization;

namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed class ConnectorLink
{
    public string UniqueIdentifier { get; set; } = string.Empty;
    public FunctionBlockConnector Source { get; set; } = new();
    public FunctionBlockConnector Destination { get; set; } = new();
    [JsonConverter(typeof(TypeNameHandlingConverter))]
    public object? Converter { get; set; }
}
