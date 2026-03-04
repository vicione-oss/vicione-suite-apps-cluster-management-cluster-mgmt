using ClusterManagement.Backend.DeployCluster.StateMachines;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sdk.Backend.Persistence;

namespace ClusterManagement.Backend.DeployCluster.Persistence;

public class ClusterSagaDbContext(DbContextOptions options) : SagaDbContext(options), IClusterSagaDbContext
{
    internal const string DbSchemaName = "clustersaga";

    public string DefaultSchemaName => DbSchemaName;
    public IEnumerable<Type> NotSynchronizedEntityTypes => [];

    public Task MigrateAsync(CancellationToken cancellationToken = default)
        => Database.MigrateAsync(cancellationToken);

    public DbSet<DeployClusterState> DeployClusterState { get; set; }

    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get
        {
            yield return new DeployClusterClassMap();
        }
    }

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchemaName);
        base.OnModelCreating(modelBuilder);
    }
}

public sealed class ClusterSagaDbContextPostgres(DbContextOptions<ClusterSagaDbContextPostgres> options) :
    ClusterSagaDbContext(options), IPostgresDbContext
{
}

public sealed class ClusterSagaDbContextSqlite(DbContextOptions<ClusterSagaDbContextSqlite> options) :
    ClusterSagaDbContext(options), ISqliteDbContext
{
}

public sealed class ClusterSagaDbContextPostgresFactory : IDesignTimeDbContextFactory<ClusterSagaDbContextPostgres>
{
    public ClusterSagaDbContextPostgres CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ClusterSagaDbContextPostgres>();
        optionsBuilder.UseNpgsql();
        return new ClusterSagaDbContextPostgres(optionsBuilder.Options);
    }
}

public sealed class ClusterSagaDbContextSqliteFactory : IDesignTimeDbContextFactory<ClusterSagaDbContextSqlite>
{
    public ClusterSagaDbContextSqlite CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ClusterSagaDbContextSqlite>();
        optionsBuilder.UseSqlite();
        return new ClusterSagaDbContextSqlite(optionsBuilder.Options);
    }
}
