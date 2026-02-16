using Bunit;
using ClusterManagement.Public;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Infrastructure;
using Sdk.Testing.Client;
using ViciOne.Cluster.Model;
using ClusterManagement.Client.Services;
using ClusterManagement.Tests.Tools;

namespace ClusterManagement.Client.Tests.Services;

public class ClusterServiceTests
{
    private static ClusterService SetupService(BunitContext ctx, List<Cluster> clusters, Action<IUiMediator>? mediatorMockCallback = null)
    {
        ctx.SetupSuiteServices(setup =>
        {
            setup.ClientMediator.Request<GetClusterInfosRequest, GetClusterInfosResponse>(
                Arg.Any<GetClusterInfosRequest>(), Arg.Any<CancellationToken>())
                .Returns(new GetClusterInfosResponse
                {
                    Results = [.. clusters.Select(c => new ClusterInfo(c.Id, c.Version, c.Name))]
                });

            mediatorMockCallback?.Invoke(setup.ClientMediator);
        });

        ctx.Services.AddSingleton<ClusterService>();
        return ctx.Services.GetRequiredService<ClusterService>();
    }

    [Fact]
    public async Task Remove_cluster_should_not_fail()
    {
        // Arrange
        using var ctx = new BunitContext();
        var service = SetupService(ctx, []);

        // Act + Assert
        await service.Remove(Guid.NewGuid(), new Version(1, 0, 0), Xunit.TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Query_cluster_versions_should_return_empty_list_for_empty_storage()
    {
        // Arrange
        using var ctx = new BunitContext();
        var service = SetupService(ctx, []);

        // Act
        var clusters = await service.QueryVersions(Xunit.TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(clusters);
    }

    [Fact]
    public async Task Query_cluster_versions_should_return_existing_versions()
    {
        // Arrange
        using var ctx = new BunitContext();
        var service = SetupService(ctx,
        [
            ClusterDbContextTools.CreateCluster(),
            ClusterDbContextTools.CreateCluster(),
        ]);

        // Act
        var clusters = await service.QueryVersions(Xunit.TestContext.Current.CancellationToken);

        // Assert
        clusters.Should().HaveCount(2);
    }

    [Fact]
    public async Task Query_cluster_with_id_and_version_should_return_existing_cluster()
    {
        // Arrange
        var clusters = new List<Cluster>
        {
            ClusterDbContextTools.CreateCluster(),
            ClusterDbContextTools.CreateCluster(),
        };

        using var ctx = new BunitContext();
        var service = SetupService(ctx, clusters,
            mediatorMock =>
            {
                mediatorMock.Request<GetClusterRequest, GetClusterResponse>(
                        Arg.Any<GetClusterRequest>(), Arg.Any<CancellationToken>())
                    .Returns(new GetClusterResponse
                    {
                        CompressedCluster = ClusterSerializer.Compress(clusters[0])
                    });
            });

        // Act
        var cluster = await service.Query(clusters[0].Id, clusters[0].Version, Xunit.TestContext.Current.CancellationToken);

        // Assert
        cluster.Should().NotBeNull().And.BeEquivalentTo(clusters[0]);
    }

}
