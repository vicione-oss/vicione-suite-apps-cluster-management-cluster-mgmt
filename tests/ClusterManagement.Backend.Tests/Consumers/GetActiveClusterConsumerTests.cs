using ClusterManagement.Backend.Consumers;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public;
using ClusterManagement.Public.Requests;
using ClusterManagement.Tests.Tools;
using ClusterManagement.Tests.Tools.Extensions;
using ClusterManagement.Tests.Tools.Factories;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Backend;
using ViciOne.Cluster.Builder;
using Xunit;

namespace ClusterManagement.Backend.Tests.Consumers;

public class GetActiveClusterConsumerTests : TestWithDbContextSqlite<ClusterDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IClusterSnapshotStore _snapshotStore = Substitute.For<IClusterSnapshotStore>();

    public GetActiveClusterConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetActiveClusterConsumer>();
            cfg.AddSingleton<IClusterDbContext>(_ => TestDbContext);
            cfg.AddSingleton(_snapshotStore);
            cfg.AddSingleton(Substitute.For<ILogger<GetActiveClusterConsumer>>());
        };

    [Fact]
    public async Task Request_unknown_clusterId_should_return_error()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetActiveClusterRequest(Guid.NewGuid());

        // Act
        var response = await tester.TestRequest<GetActiveClusterResponse, GetActiveClusterRequest>(request);

        // Assert
        response.CompressedCluster.Should().BeNullOrEmpty();
        response.RequestError.Should().NotBeNull();
    }

    [Fact]
    public async Task Request_existing_clusterId_should_return_cluster_json()
    {
        // Arrange        
        using var builder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Complex);
        await using var tester = new MassTransitTester(_configureServices);

        var dbContext = tester.Services.GetRequiredService<IClusterDbContext>();
        dbContext.SaveActiveClusterVersion(builder.Cluster);
        _snapshotStore.SetupLoad(builder.Cluster);

        var request = new GetActiveClusterRequest(builder.Cluster.Id);

        // Act
        var response = await tester.TestRequest<GetActiveClusterResponse, GetActiveClusterRequest>(request);

        // Assert
        response.CompressedCluster.Should().NotBeNullOrEmpty();
        response.RequestError.Should().BeNull();

        var deserialized = await ClusterSerializer.Decompress(response.CompressedCluster!);
        using var resultBuilder = new ClusterBuilder(deserialized, builder.DependencyResolver);

        builder.CacheShouldBeEquivalent(resultBuilder);
    }
}
