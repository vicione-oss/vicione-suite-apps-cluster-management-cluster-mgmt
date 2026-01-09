using ClusterManagement.Public.ClusterEditor;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Persistence;

namespace ClusterManagement.Backend.DbContext;

public interface IClusterEditorDbContext : IModuleDbContext
{
    DbSet<IoddSettings> IoddSettings { get; }
    DbSet<Settings> Settings { get; }
}
