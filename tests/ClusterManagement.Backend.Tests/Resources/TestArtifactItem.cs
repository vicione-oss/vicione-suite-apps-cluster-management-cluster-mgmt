using System.Diagnostics;
using System.Text.Json.Serialization;
using Sdk.Backend.ArtifactApi;

namespace ClusterManagement.Backend.Tests.Resources;

[DebuggerDisplay("Repo = {Repo,nq}, Path = {Path,nq}, Name = {Name,nq}")]
internal class TestArtifactItem : IArtifactItem
{
    public IArtifactChecksum? Checksum { get; set; }

    [JsonPropertyName("modified")]
    public DateTimeOffset? Modified { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("repo")]
    public string Repo { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long? Size { get; set; }
}

