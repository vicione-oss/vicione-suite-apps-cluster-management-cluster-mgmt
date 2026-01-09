using ViciOne.Cluster.Model;

namespace ClusterManagement.Public.Commands;

public sealed record CommitClusterMetaInfo
{
    public Guid ClusterId { get; init; }
    public Version ClusterVersion { get; init; }
    public string Name { get; init; }
    public string? Description { get; init; }

    /// <summary>
    /// needed for serialization
    /// </summary>
    public CommitClusterMetaInfo()
    {
        ClusterVersion = new Version();
        Name = string.Empty;
    }

    public CommitClusterMetaInfo(Cluster cluster)
    {
        ClusterId = cluster.Id;
        ClusterVersion = cluster.Version;
        Name = cluster.Name;
        Description = cluster.Description;
    }
}
