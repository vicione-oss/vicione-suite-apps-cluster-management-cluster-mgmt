namespace ClusterManagement.Backend.Contracts;

/// <summary>
/// Basic metadata class used for EngineHost/FunctionBlock/DataPort metadata json
/// </summary>
public record ArtifactMetadata
{
    public required string Name { get; set; }

    public required string Version { get; set; }

    public required string MinCoreVersion { get; set; }
}
