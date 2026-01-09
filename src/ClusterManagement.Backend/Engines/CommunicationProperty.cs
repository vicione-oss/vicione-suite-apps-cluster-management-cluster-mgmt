using System.Text.Json.Serialization;

namespace ClusterManagement.Backend.Engines;

public sealed record class CommunicationProperty(string Id)
{
    [JsonConverter(typeof(TypeNameHandlingConverter))]
    public TypedObject? Value { get; set; }
}
