using ClusterManagement.Public.Requests;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Snapshots;

public static class ClusterInfoFactory
{
    public static ClusterInfo CreateInfo(Cluster cluster, bool isActive = false)
        => new(cluster.Id, cluster.Version, cluster.Name, isActive)
        {
            Description = cluster.Description,
        };

    public static ClusterInfo CreateInfo(ClusterInfo info, bool isActive = false)
        => new(info.Id, info.Version, info.Name, isActive)
        {
            Description = info.Description,
        };
}
