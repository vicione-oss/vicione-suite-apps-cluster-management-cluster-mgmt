using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Engines;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sdk.Backend.Persistence;

namespace ClusterManagement.Backend.DbContext;

// to add a migration see readme of SDK
public class EngineDbContext : ModuleDbContext, IEngineDbContext
{
    internal const string DbSchemaName = "engines";

    public override string DefaultSchemaName => DbSchemaName;
    public DbSet<EngineHost> EngineHosts => Set<EngineHost>();
    public DbSet<EngineDeployment> EngineDeployments => Set<EngineDeployment>();

    internal EngineDbContext(DbContextOptions options)
        : base(options)
    {
    }

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

public sealed class EngineDbContextPostgres(DbContextOptions<EngineDbContextPostgres> options) : EngineDbContext(options), IPostgresDbContext
{
}

public sealed class EngineDbContextSqlite(DbContextOptions<EngineDbContextSqlite> options) : EngineDbContext(options), ISqliteDbContext
{
}

public sealed class EngineDbContextPostgresFactory : IDesignTimeDbContextFactory<EngineDbContextPostgres>
{
    public EngineDbContextPostgres CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EngineDbContextPostgres>();
        optionsBuilder.UseNpgsql();
        return new EngineDbContextPostgres(optionsBuilder.Options);
    }
}

public sealed class EngineDbContextSqliteFactory : IDesignTimeDbContextFactory<EngineDbContextSqlite>
{
    public EngineDbContextSqlite CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EngineDbContextSqlite>();
        optionsBuilder.UseSqlite();
        return new EngineDbContextSqlite(optionsBuilder.Options);
    }
}
