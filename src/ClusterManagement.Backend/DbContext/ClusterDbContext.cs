using System.Text.Json;
using System.Text.Json.Serialization;
using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sdk.Backend.Persistence;

namespace ClusterManagement.Backend.DbContext;

// to add a migration see readme of SDK
[ModuleDbContext(DefaultSchemaName = DbSchemaName)]
public partial class ClusterDbContext(DbContextOptions options) : ModuleDbContext(options), IClusterDbContext
{
    internal const string DbSchemaName = "cluster";

    public DbSet<ActiveClusterInfo> ActiveClusters { get; set; }

    internal static JsonSerializerOptions ContextJsonOptions { get; } =
        new()
        {
            Converters =
            {
                new TypeNameHandlingConverter()
            }
        };

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
