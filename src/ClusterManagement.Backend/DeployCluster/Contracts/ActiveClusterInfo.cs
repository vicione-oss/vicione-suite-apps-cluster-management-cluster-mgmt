namespace ClusterManagement.Backend.DeployCluster.Contracts;

public class ActiveClusterInfo
{
    /// <summary>
    /// <see cref="DeployedBy"/> of a version the suite activated itself.
    /// </summary>
    public const string DeployedBySuite = "suite";

    public required Guid Id { get; init; }

    public required Version Version { get; set; }

    public DateTimeOffset Deployed { get; set; }

    public string? DeployedBy { get; set; }
}
