using ClusterManagement.Internal.Contracts;
using Microsoft.EntityFrameworkCore;
using Sdk.Backend.Persistence;

namespace ClusterManagement.Backend.DbContext;

public interface IClusterEditorDbContext : IModuleDbContext
{
    DbSet<IoddSettings> IoddSettings { get; }
    DbSet<EditorSettings> Settings { get; }
}
