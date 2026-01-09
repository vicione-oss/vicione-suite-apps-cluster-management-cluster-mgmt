using System.Text.Json.Serialization;

namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed class PoolingOptions
{
    public PoolingStrategy Strategy { get; set; }
    [JsonConverter(typeof(TypeNameHandlingConverter))]
    public object? AggregatingPooling { get; set; }
}
