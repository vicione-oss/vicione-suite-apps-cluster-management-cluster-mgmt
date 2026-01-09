using System.Text.Json.Serialization;

namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed record class CommunicationProperty(string Id)
{
    [JsonConverter(typeof(TypeNameHandlingConverter))]
    public object? Value { get; set; }
}
