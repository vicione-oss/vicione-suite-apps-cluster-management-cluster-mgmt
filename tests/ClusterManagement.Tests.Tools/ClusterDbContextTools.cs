using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.DeployCluster.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Tests.Tools;

public static class ClusterDbContextTools
{
    public static IClusterDbContext CreateClusterContextPostgres(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<ClusterDbContext>()
            .UseNpgsql(connectionString);

        return new ClusterDbContext(builder.Options);
    }

    public static IClusterSagaDbContext CreateClusterSagaContextPostgres(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<ClusterSagaDbContext>()
            .UseNpgsql(connectionString);

        return new ClusterSagaDbContext(builder.Options);
    }

    public static async Task SetupDbContextScoped<TInterface, TContext>(this IServiceProvider serviceProvider,
        Action<TContext>? context = null)
        where TInterface : notnull
        where TContext : DbContext
    {
        using var scope = serviceProvider.CreateScope();

        var interfaceDb = scope.ServiceProvider.GetRequiredService<TInterface>();
        if (interfaceDb is TContext dbContext)
        {
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();

            context?.Invoke(dbContext);
            return;
        }

        throw new InvalidOperationException("Context is not of right type");
    }

    public static ActiveClusterInfo SaveActiveClusterVersion(this IClusterDbContext context, Cluster cluster)
    {
        if (context.ActiveClusters.Any(k => k.Id == cluster.Id))
            throw new InvalidOperationException("Cluster is already seeded");

        var entity = context.ActiveClusters.Add(new ActiveClusterInfo
        {
            Id = cluster.Id,
            Version = cluster.Version,
            Deployed = DateTimeOffset.UtcNow
        });
        context.SaveChanges();

        entity.State = EntityState.Detached;
        return entity.Entity;
    }

    public static Cluster CreateCluster(Guid? clusterId = null, Version? version = null)
        => new()
        {
            Id = clusterId ?? Guid.NewGuid(),
            Description = "Description",
            Name = "Name",
            Icon = "123",
            Version = version ?? new Version(1, 0, 0),
            VersionCreated = DateTime.UtcNow,
            Dataflows = [new Dataflow()
            {
                Name = "Dataflow",
                Root = new Container
                {
                    Id = Guid.NewGuid(),
                    Name = "RootContainer",
                },
            }],
        };
}
