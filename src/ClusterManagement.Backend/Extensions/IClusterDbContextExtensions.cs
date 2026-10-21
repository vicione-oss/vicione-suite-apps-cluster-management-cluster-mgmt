using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Contracts;

namespace ClusterManagement.Backend.Extensions;

internal static class IClusterDbContextExtensions
{
    public static async Task<int> UpsertActiveClusterInfo(this IClusterDbContext dbContext, Guid clusterId, Version clusterVersion, DateTimeOffset deployed, string? deployedBy = ActiveClusterInfo.DeployedBySuite, CancellationToken cancellationToken = default)
    {
        var activeCluster = await dbContext.ActiveClusters.FindAsync([clusterId], cancellationToken: cancellationToken);
        if (activeCluster is not null)
        {
            activeCluster.Version = clusterVersion;
            activeCluster.Deployed = deployed;
            activeCluster.DeployedBy = deployedBy;
        }
        else
        {
            dbContext.ActiveClusters.Add(new ActiveClusterInfo()
            {
                Id = clusterId,
                Version = clusterVersion,
                Deployed = deployed,
                DeployedBy = deployedBy,
            });
        }

        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}
