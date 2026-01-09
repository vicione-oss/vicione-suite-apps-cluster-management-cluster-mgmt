using System.Text.Json.Serialization;
using ClusterManagement.Backend.Extensions;

namespace ClusterManagement.Backend.EngineHosts.CommunicationContracts;

public sealed class DeployRequest : IDeployRequest
{
    [JsonConverter(typeof(PreserveTypeJsonConverter))]
    public object StartParameter { get; set; } = new();
    public IReadOnlyCollection<PackageReference> PackageReferences { get; set; } = [];
    public int EngineChainIndex { get; set; }
    public uint CycleTime { get; set; }
}
