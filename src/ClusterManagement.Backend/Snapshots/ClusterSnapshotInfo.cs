using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Snapshots;

public sealed record ClusterSnapshotInfo(Guid Id, Version Version, string Name)
{
    public string? Description { get; init; }

    public List<ClusterDependency>? Dependencies { get; init; }
}
