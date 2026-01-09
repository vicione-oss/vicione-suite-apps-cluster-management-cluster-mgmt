using ClusterManagement.Backend.DeployCluster.Contracts;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Persistence;

namespace ClusterManagement.Backend.DbContext;

public interface IClusterDbContext : IModuleDbContext
{
    DbSet<ActiveClusterInfo> ActiveClusters { get; set; }
}
