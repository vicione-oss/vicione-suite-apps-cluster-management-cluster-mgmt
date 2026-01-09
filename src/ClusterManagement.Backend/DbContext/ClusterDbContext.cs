using System.Text.Json;
using System.Text.Json.Serialization;
using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sdk.Backend.Persistence;

namespace ClusterManagement.Backend.DbContext;

// to add a migration see readme of SDK
public class ClusterDbContext : ModuleDbContext, IClusterDbContext
{
    internal const string DbSchemaName = "cluster";
    public override string DefaultSchemaName => DbSchemaName;

    public DbSet<ActiveClusterInfo> ActiveClusters { get; set; }

    internal static JsonSerializerOptions ContextJsonOptions { get; } =
        new()
        {
            Converters =
            {
                new TypeNameHandlingConverter()
            }
        };

    internal ClusterDbContext(DbContextOptions options)
        : base(options)
    {
    }

    protected override void OnModuleModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActiveClusterInfo>()
            .Property(c => c.Version)
            .PersistAsString();

        modelBuilder.Entity<ActiveClusterInfo>()
            .HasKey(c => c.Id);
    }
}

public sealed class ObjectToJsonConverter<T> : ValueConverter<T?, string>
    where T : new()
{
    public ObjectToJsonConverter()
        : base(v => JsonSerializer.Serialize(v, ClusterDbContext.ContextJsonOptions),
            v => JsonSerializer.Deserialize<T>(v, ClusterDbContext.ContextJsonOptions))
    {
    }
}

public sealed class ClusterDbContextPostgres(DbContextOptions<ClusterDbContextPostgres> options) : ClusterDbContext(options), IPostgresDbContext
{
}

public sealed class ClusterDbContextSqlite(DbContextOptions<ClusterDbContextSqlite> options) : ClusterDbContext(options), ISqliteDbContext
{
}

public sealed class ClusterDbContextPostgresFactory : IDesignTimeDbContextFactory<ClusterDbContextPostgres>
{
    public ClusterDbContextPostgres CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ClusterDbContextPostgres>();
        optionsBuilder.UseNpgsql();
        return new ClusterDbContextPostgres(optionsBuilder.Options);
    }
}

public sealed class ClusterDbContextSqliteFactory : IDesignTimeDbContextFactory<ClusterDbContextSqlite>
{
    public ClusterDbContextSqlite CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ClusterDbContextSqlite>();
        optionsBuilder.UseSqlite();
        return new ClusterDbContextSqlite(optionsBuilder.Options);
    }
}
