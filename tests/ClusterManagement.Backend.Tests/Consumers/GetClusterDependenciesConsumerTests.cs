using ClusterManagement.Backend.Consumers;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Testing.Backend;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests.Consumers;

public class GetClusterDependenciesConsumerTests : TestWithDbContextSqlite<ClusterDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IDesignProvider _fbProviderMock = Substitute.For<IDesignProvider>();

    public GetClusterDependenciesConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetClusterDependenciesConsumer>();
            cfg.AddSingleton(_ => _fbProviderMock);
        };

    [Fact]
    public async Task Request_should_return_cluster_dependencies()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var request = new GetClusterDependenciesRequest();

        var testData = new List<ClusterDependency>
        {
            new()
            {
                Name = "Chocolate",
                Version = "1.0.0"
            },
            new()
            {
                Name = "Cheese",
                Version = "0.4.7"
            },
            new()
            {
                Name = "Cake",
                Version = "2.1.3"
            },
        };

        _fbProviderMock.GetClusterDependencies()
            .Returns(testData);

        // Act
        var response = await tester.TestRequest<GetClusterDependenciesResponse, GetClusterDependenciesRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.Dependencies.Should().BeEquivalentTo(testData);
    }
}
