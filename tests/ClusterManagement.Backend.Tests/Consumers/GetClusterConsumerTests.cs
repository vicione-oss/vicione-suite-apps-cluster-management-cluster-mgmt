using ClusterManagement.Backend.Consumers;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public;
using ClusterManagement.Public.Requests;
using ClusterManagement.Tests.Tools.Factories;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace ClusterManagement.Backend.Tests.Consumers;
public class GetClusterConsumerTests : TestWithDbContextSqlite<ClusterDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IClusterSnapshotStore _snapshotStoreMock = Substitute.For<IClusterSnapshotStore>();
    public GetClusterConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetClusterConsumer>();
            cfg.AddSingleton(_snapshotStoreMock);
            cfg.AddSingleton(Substitute.For<ILogger<GetClusterConsumer>>());
        };

    [Fact]
    public async Task Request_unknown_clusterId_should_return_error()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterRequest(Guid.NewGuid(), new Version());

        // Act
        var response = await tester.TestRequest<GetClusterResponse, GetClusterRequest>(request);

        // Assert
        response.RequestError.Should().NotBeNull();
        response.CompressedCluster.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task GetCluster_should_return_requested_json()
    {
        //Arrange
        await using var tester = new MassTransitTester(_configureServices);
        using var builder = ClusterFactory.CreateComplexCluster(ClusterComplexity.Complex);
        var clusterCompressed = ClusterSerializer.Compress(builder.Cluster);
        _snapshotStoreMock
            .LoadCompressedJson(Arg.Is(builder.Cluster.Id), Arg.Is(builder.Cluster.Version), Arg.Any<CancellationToken>())
            .Returns(clusterCompressed);
        var requestWithVersion = new GetClusterRequest(builder.Cluster.Id, builder.Cluster.Version);

        //Act
        var responseWithVersion = await tester.TestRequest<GetClusterResponse, GetClusterRequest>(requestWithVersion);

        //Assert
        responseWithVersion.RequestError.Should().BeNull();
        responseWithVersion.CompressedCluster.Should().BeEquivalentTo(clusterCompressed);
    }
}
