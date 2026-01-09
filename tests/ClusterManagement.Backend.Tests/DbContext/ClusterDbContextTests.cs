using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;
using Xunit;

namespace ClusterManagement.Backend.Tests.DbContext;

public class ClusterDbContextTests : TestWithDbContextSqlite<ClusterDbContextSqlite>
{
    [Fact]
    public void Insert_active_cluster_should_succeed()
    {
        // Arrange
        using var dbContext = new ServiceCollection()
            .AddScoped(_ => (IClusterDbContext)TestDbContext)
            .BuildServiceProvider()
            .GetRequiredService<IClusterDbContext>();

        // Act
        dbContext.ActiveClusters.Add(new ActiveClusterInfo() { Id = new Guid(), Version = new Version(0, 0, 1), Deployed = DateTimeOffset.UtcNow });
    }
}
