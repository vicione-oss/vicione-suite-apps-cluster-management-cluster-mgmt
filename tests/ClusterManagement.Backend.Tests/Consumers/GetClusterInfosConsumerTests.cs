using ClusterManagement.Backend.Consumers;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Requests;
using ClusterManagement.Tests.Tools;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Testing.Backend;
using ViciOne.Cluster.Model;


namespace ClusterManagement.Backend.Tests.Consumers;

public class GetClusterInfosConsumerTests : TestWithDbContextSqlite<ClusterDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IClusterSnapshotStore _snapshotStoreMock = Substitute.For<IClusterSnapshotStore>();

    public GetClusterInfosConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetClusterInfosConsumer>();
            cfg.AddSingleton<IClusterDbContext>(_ => TestDbContext);
            cfg.AddSingleton(_snapshotStoreMock);
            cfg.AddSingleton(Substitute.For<ILogger<GetClusterInfosConsumer>>());
        };

    [Fact]
    public async Task Request_without_id_should_return_all_infos()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterInfosRequest();
        var dbContext = tester.Services.GetRequiredService<IClusterDbContext>();

        var cluster1 = new Cluster { Id = Guid.NewGuid(), Version = new(1, 0, 2) };
        var cluster2 = new Cluster { Id = Guid.NewGuid(), Version = new(1, 3, 1) };

        dbContext.SaveActiveClusterVersion(cluster1);
        dbContext.SaveActiveClusterVersion(cluster2);

        var testData = new List<ClusterInfo>
        {
            new(cluster1.Id, cluster1.Version, "Test1 Active"),
            new(cluster1.Id, new(1, 0, 4), "Test1 A"),
            new(cluster1.Id, new(1, 1, 0), "Test1 B"),
            new(cluster1.Id, new(1, 0, 5), "Test1 D"),
            new(cluster2.Id, new(1, 5, 0), "Test2 E"),
            new(cluster2.Id, new(1, 2, 6), "Test2 F"),
            new(cluster2.Id, cluster2.Version, "Test2 Active"),
        };

        _snapshotStoreMock
            .GetClusterInfos(null, Arg.Any<CancellationToken>())
            .Returns(testData);

        // Act
        var response = await tester.TestRequest<GetClusterInfosResponse, GetClusterInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.Results.Count(k => k.IsActive).Should().Be(2);
        response.Results.Should()
            .BeEquivalentTo(testData,
                options =>
                    options.Excluding(k => k.IsActive).Excluding(k => k.Deployed),
                "2 are active");
    }

    [Fact]
    public async Task Active_version_carries_its_deployment_time()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var dbContext = tester.Services.GetRequiredService<IClusterDbContext>();
        var cluster = new Cluster { Id = Guid.NewGuid(), Version = new(1, 0, 2) };
        var active = dbContext.SaveActiveClusterVersion(cluster);

        _snapshotStoreMock
            .GetClusterInfos(null, Arg.Any<CancellationToken>())
            .Returns([new(cluster.Id, cluster.Version, "Active"), new(cluster.Id, new(1, 0, 3), "Draft")]);

        // Act
        var response = await tester.TestRequest<GetClusterInfosResponse, GetClusterInfosRequest>(new GetClusterInfosRequest());

        // Assert
        response.Results.Should().ContainSingle(k => k.Deployed != null)
            .Which.Should().Match<ClusterInfo>(k => k.Version == cluster.Version && k.Deployed == active.Deployed);
    }

    [Fact]
    public async Task Request_with_id_should_return_requested_cluster_infos()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var clusterId1 = Guid.NewGuid();
        var clusterId2 = Guid.NewGuid();
        var request = new GetClusterInfosRequest
        {
            ClusterId = clusterId2
        };
        var testData = CreateTestData(clusterId1, clusterId2);

        _snapshotStoreMock
            .GetClusterInfos(Arg.Is(clusterId2), Arg.Any<CancellationToken>())
            .Returns(testData.Where(k => k.Id == clusterId2));

        // Act
        var response = await tester.TestRequest<GetClusterInfosResponse, GetClusterInfosRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.Results.Should().BeEquivalentTo(testData.Where(k => k.Id == clusterId2));
    }

    private static List<ClusterInfo> CreateTestData(Guid clusterId1, Guid clusterId2)
        => [
            new(clusterId1, new(1, 0, 0), "Test1 A"),
            new(clusterId1, new(1, 1, 0), "Test1 B"),
            new(clusterId1, new(1, 0, 5), "Test1 C"),
            new(clusterId2, new(1, 5, 0), "Test2 E"),
            new(clusterId2, new(1, 2, 6), "Test2 F"),
        ];
}
