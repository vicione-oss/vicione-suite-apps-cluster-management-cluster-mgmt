using AwesomeAssertions;
using ClusterManagement.Backend.DeployCluster.Consumers;
using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Public.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

namespace ClusterManagement.Backend.Tests.DeployCluster.Consumers;

public class GetDeployClusterStateConsumerTests : TestWithDbContextSqlite<ClusterSagaDbContextSqlite>
{
    private void SetupConsumer(IBusRegistrationConfigurator cfg)
    {
        cfg.AddConsumer<GetDeployClusterStateConsumer>();
        cfg.AddSingleton<IClusterSagaDbContext>(_ => TestDbContext);
        cfg.AddSingleton(Substitute.For<ILogger<GetDeployClusterStateConsumer>>());
    }

    [Fact]
    public async Task Should_return_deployment_state_if_cluster_deployment_is_running()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg));
        var clusterId = Guid.NewGuid();
        var currentState = 2;
        var activeVersion = new Version(1, 0, 0);
        var targetVersion = new Version(1, 2, 1);

        var request = new GetDeployClusterStateRequest(clusterId);

        var state = new DeployClusterState() { ClusterId = clusterId, CurrentState = currentState, ActiveVersion = activeVersion, TargetVersion = targetVersion };
        var test = TestDbContext.DeployClusterState.Add(state);
        await TestDbContext.SaveChangesAsync();

        // Act
        var response = await tester.TestRequest<GetDeployClusterStateResponse, GetDeployClusterStateRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.State.Should().NotBeNull();
        response.State.ClusterId.Should().Be(clusterId);
        response.State.State.Should().Be(currentState);
        response.State.ActiveVersion.Should().Be(activeVersion);
        response.State.TargetVersion.Should().Be(targetVersion);
    }

    [Fact]
    public async Task Request_with_unknown_cluster_id_should_return_empty_response()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg));
        var request = new GetDeployClusterStateRequest(Guid.NewGuid());

        // Act
        var response = await tester.TestRequest<GetDeployClusterStateResponse, GetDeployClusterStateRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.State.Should().BeNull();
    }
}
