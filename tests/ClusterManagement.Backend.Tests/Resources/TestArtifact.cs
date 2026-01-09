using System.Diagnostics;
using Sdk.Backend.Artifacts;

namespace ClusterManagement.Backend.Tests.Resources;

[DebuggerDisplay("Repo = {Repo,nq}, Path = {Path,nq}, Name = {Name,nq}")]
internal sealed class TestArtifact : IArtifact
{
    public IArtifactChecksum? Checksum { get; set; }
    public DateTimeOffset? Modified { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public long? Size { get; set; }
    public ArtifactKind Kind { get; set; } = ArtifactKind.File;

    public string SourceKey { get; set; } = string.Empty;
}

