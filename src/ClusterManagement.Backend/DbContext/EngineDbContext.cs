using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Engines;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Persistence;

namespace ClusterManagement.Backend.DbContext;

// to add a migration see readme of SDK
[ModuleDbContext(DefaultSchemaName = DbSchemaName)]
public partial class EngineDbContext : ModuleDbContext, IEngineDbContext
{
    internal const string DbSchemaName = "engines";

    public DbSet<EngineHost> EngineHosts => Set<EngineHost>();
    public DbSet<EngineDeployment> EngineDeployments => Set<EngineDeployment>();

    protected override void OnModuleModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EngineHost>().HasKey(eh => eh.Id);

        modelBuilder.Entity<EngineDeployment>(x =>
        {
            x.HasKey(static ed => ed.Id);
            x.HasOne<EngineHost>()
                .WithMany(eh => eh.Deployments)
                .HasForeignKey(ed => ed.EngineHostId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
