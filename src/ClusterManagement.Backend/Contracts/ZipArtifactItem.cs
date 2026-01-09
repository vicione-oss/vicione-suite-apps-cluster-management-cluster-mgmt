namespace ClusterManagement.Backend.Contracts;

/// <summary>
/// Represents a zip artifact on e.g. JFrog enriched with <see cref="ArtifactMetadata"/>
/// </summary>
public record ZipArtifactItem
{
    public required DateTimeOffset? Modified { get; set; }

    public required string Name { get; set; }

    public required string Path { get; set; }

    public required long? Size { get; set; }

    public required ArtifactMetadata Metadata { get; init; }
}
