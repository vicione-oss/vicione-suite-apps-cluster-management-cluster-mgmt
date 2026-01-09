using ClusterManagement.Public.Enums;

namespace ClusterManagement.Public.Commands;

public sealed record CommitClusterOptions
{
    public bool IncrementVersion { get; init; } = true;
    public bool CleanupDependencies { get; init; }

    public VersionIncrementType? IncrementType { get; init; }
}
