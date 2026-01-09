using System.Text.Json.Serialization;

namespace ClusterManagement.Backend.Engines.CommunicationContracts;

public sealed class FunctionBlockConnector
{
    public string DesignIdentifier { get; set; } = string.Empty;
    public string UniqueIdentifier { get; set; } = string.Empty;
    [JsonConverter(typeof(TypeNameHandlingConverter))]
    public object? InitialValue { get; set; }
    public bool MarkAsChangedOnlyIfNotEqual { get; set; }
    public PoolingOptions? CrossSourcePoolingOptions { get; set; }
    public PoolingOptions? PerSourcePoolingOptions { get; set; }
    [JsonConverter(typeof(TypeNameHandlingConverter))]
    public List<object> Validators { get; init; } = [];
}
