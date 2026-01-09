using System.Text.Json.Serialization;

namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed class FunctionBlockVariable
{
    public string DesignIdentifier { get; set; } = string.Empty;
    public string UniqueIdentifier { get; set; } = string.Empty;
    [JsonConverter(typeof(TypeNameHandlingConverter))]
    public object? Value { get; set; }
    public DateTime Timestamp { get; set; }
}
