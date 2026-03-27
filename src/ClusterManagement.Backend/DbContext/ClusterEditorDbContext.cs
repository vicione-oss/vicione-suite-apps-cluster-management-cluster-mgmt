using ClusterManagement.Internal.Contracts;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Persistence;

namespace ClusterManagement.Backend.DbContext;

// to add a migration see readme of SDK
[ModuleDbContext(DefaultSchemaName = DbSchemaName)]
public partial class ClusterEditorDbContext : ModuleDbContext, IClusterEditorDbContext
{
    internal const string DbSchemaName = "clustereditor";

    public DbSet<EditorSettings> Settings => Set<EditorSettings>();
    public DbSet<IoddSettings> IoddSettings => Set<IoddSettings>();

    protected override void OnModuleModelCreating(ModelBuilder modelBuilder)
       => modelBuilder.Entity<EditorSettings>().HasKey(s => s.UserId);
}
