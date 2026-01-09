using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Engines;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Persistence;

namespace ClusterManagement.Backend.DbContext;

public interface IEngineDbContext : IModuleDbContext
{
    DbSet<EngineHost> EngineHosts { get; }
    DbSet<EngineDeployment> EngineDeployments { get; }
}
