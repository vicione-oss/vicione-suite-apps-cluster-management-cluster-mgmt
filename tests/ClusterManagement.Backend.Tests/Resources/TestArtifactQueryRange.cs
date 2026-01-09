using System.Text.Json.Serialization;
using Sdk.Backend.Artifacts;

namespace ClusterManagement.Backend.Tests.Resources;

internal sealed class TestArtifactQueryRange : IArtifactQueryRange
{
    [JsonPropertyName("start_pos")]
    public int StartPosition { get; set; }

    [JsonPropertyName("end_pos")]
    public int EndPosition { get; set; }

    public int Total { get; set; }

    public string Source { get; set; } = string.Empty;
}
