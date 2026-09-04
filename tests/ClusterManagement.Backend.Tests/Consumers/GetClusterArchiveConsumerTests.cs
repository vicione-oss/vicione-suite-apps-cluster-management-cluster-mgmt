using ClusterManagement.Backend.Consumers;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;

namespace ClusterManagement.Backend.Tests.Consumers;

public class GetClusterArchiveConsumerTests : TestWithDbContextSqlite<ClusterDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IClusterSnapshotStore _snapshotStoreMock = Substitute.For<IClusterSnapshotStore>();

    public GetClusterArchiveConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetClusterArchiveConsumer>();
            cfg.AddSingleton(_ => _snapshotStoreMock);
        };

    [Fact]
    public async Task Request_for_unknown_cluster_should_return_empty_response()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterArchiveRequest(Guid.NewGuid(), new Version(1, 0, 0));

        // Act
        var response = await tester.TestRequest<GetClusterArchiveResponse, GetClusterArchiveRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.ArchiveRaw.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Request_existing_clusterId_should_return_zip_archive()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var clusterId = Guid.NewGuid();
        var clusterVersion = new Version(1, 0, 0);
        var request = new GetClusterArchiveRequest(clusterId, clusterVersion);

        var data = "HTTP/1.1 200 OK"u8.ToArray();

        _snapshotStoreMock
            .LoadCompressedJson(Arg.Is(clusterId), Arg.Is(clusterVersion), Arg.Any<CancellationToken>())
            .Returns(data);

        // Act
        var response = await tester.TestRequest<GetClusterArchiveResponse, GetClusterArchiveRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.ArchiveRaw.Should().BeEquivalentTo(data);
    }
}
