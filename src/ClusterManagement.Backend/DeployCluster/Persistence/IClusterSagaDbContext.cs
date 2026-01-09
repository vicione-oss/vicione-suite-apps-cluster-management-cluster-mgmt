using ClusterManagement.Backend.DeployCluster.StateMachines;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Persistence;

namespace ClusterManagement.Backend.DeployCluster.Persistence;

public interface IClusterSagaDbContext : IModuleDbContext
{
    DbSet<DeployClusterState> DeployClusterState { get; }
}
