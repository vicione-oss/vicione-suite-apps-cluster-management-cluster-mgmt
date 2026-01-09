using ClusterManagement.Internal.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sdk.Backend.Persistence;

namespace ClusterManagement.Backend.DbContext;

// to add a migration see readme of SDK
public class ClusterEditorDbContext : ModuleDbContext, IClusterEditorDbContext
{
    internal const string DbSchemaName = "clustereditor";

    public override string DefaultSchemaName => DbSchemaName;
    public DbSet<EditorSettings> Settings => Set<EditorSettings>();
    public DbSet<IoddSettings> IoddSettings => Set<IoddSettings>();

    internal ClusterEditorDbContext(DbContextOptions options)
        : base(options)
    { }

    protected override void OnModuleModelCreating(ModelBuilder modelBuilder)
       => modelBuilder.Entity<EditorSettings>().HasKey(s => s.UserId);
}

public sealed class ClusterEditorDbContextPostgres(DbContextOptions<ClusterEditorDbContextPostgres> options) : ClusterEditorDbContext(options), IPostgresDbContext
{ }

public sealed class ClusterEditorDbContextSqlite(DbContextOptions<ClusterEditorDbContextSqlite> options) : ClusterEditorDbContext(options), ISqliteDbContext
{ }

public sealed class ClusterEditorDbContextPostgresFactory : IDesignTimeDbContextFactory<ClusterEditorDbContextPostgres>
{
    public ClusterEditorDbContextPostgres CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ClusterEditorDbContextPostgres>();
        optionsBuilder.UseNpgsql();
        return new ClusterEditorDbContextPostgres(optionsBuilder.Options);
    }
}

public sealed class ClusterEditorDbContextSqliteFactory : IDesignTimeDbContextFactory<ClusterEditorDbContextSqlite>
{
    public ClusterEditorDbContextSqlite CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ClusterEditorDbContextSqlite>();
        optionsBuilder.UseSqlite();
        return new ClusterEditorDbContextSqlite(optionsBuilder.Options);
    }
}
