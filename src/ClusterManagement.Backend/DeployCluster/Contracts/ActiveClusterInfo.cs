namespace ClusterManagement.Backend.DeployCluster.Contracts;

public class ActiveClusterInfo
{
    public required Guid Id { get; init; }

    public required Version Version { get; set; }

    public DateTimeOffset Deployed { get; set; }

    public string? DeployedBy { get; set; }
}
