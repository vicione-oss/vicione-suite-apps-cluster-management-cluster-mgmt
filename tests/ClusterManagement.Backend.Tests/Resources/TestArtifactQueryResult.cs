using System.Text.Json.Serialization;
using Sdk.Backend.ArtifactApi;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Tests.Resources;

internal class TestArtifactQueryResult : IArtifactQueryResult
{
    /// <inheritdoc />
    [JsonIgnore]
    public IReadOnlyCollection<IArtifactItem> Artifacts => [.. Results];

    [JsonPropertyName("results")]
    public List<TestArtifactItem> Results { get; set; } = [];

    /// <inheritdoc />
    public ErrorInfo? ErrorInfo { get; set; }
}

