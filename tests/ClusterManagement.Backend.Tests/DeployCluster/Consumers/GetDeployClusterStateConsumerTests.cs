using ClusterManagement.Backend.DeployCluster.Consumers;
using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Public.Requests;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Testing.Backend;

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

        var correlationId = Guid.NewGuid();
        var state = new DeployClusterState() { CorrelationId = correlationId, ClusterId = clusterId, CurrentState = currentState, ActiveVersion = activeVersion, TargetVersion = targetVersion };
        var test = TestDbContext.DeployClusterState.Add(state);
        await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await tester.TestRequest<GetDeployClusterStateResponse, GetDeployClusterStateRequest>(request);

        // Assert
        response.RequestError.Should().BeNull();
        response.State.Should().NotBeNull();
        response.State.ClusterId.Should().Be(clusterId);
        response.State.CurrentState.Should().Be(DeployClusterStateMachine.GetState(currentState));
        response.State.ActiveVersion.Should().Be(activeVersion);
        response.State.TargetVersion.Should().Be(targetVersion);
        response.State.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task Should_return_the_newest_deployment_state_when_earlier_versions_left_several()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg));
        var clusterId = Guid.NewGuid();
        var newerTarget = new Version(1, 4, 0);

        TestDbContext.DeployClusterState.AddRange(
            new DeployClusterState { CorrelationId = Guid.NewGuid(), ClusterId = clusterId, CurrentState = 7, TargetVersion = new(1, 3, 0), Started = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc) },
            new DeployClusterState { CorrelationId = Guid.NewGuid(), ClusterId = clusterId, CurrentState = 5, TargetVersion = newerTarget, Started = new DateTime(2026, 10, 1, 8, 1, 0, DateTimeKind.Utc) });
        await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await tester.TestRequest<GetDeployClusterStateResponse, GetDeployClusterStateRequest>(new GetDeployClusterStateRequest(clusterId));

        // Assert
        response.RequestError.Should().BeNull();
        response.State.Should().NotBeNull();
        response.State.TargetVersion.Should().Be(newerTarget);
    }

    [Fact]
    public async Task Should_return_the_commit_version_as_target_while_it_is_committed()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg));
        var clusterId = Guid.NewGuid();
        var commitVersion = new Version(1, 2, 2);

        TestDbContext.DeployClusterState.Add(new DeployClusterState
        {
            CorrelationId = Guid.NewGuid(),
            ClusterId = clusterId,
            CurrentState = 3,
            ActiveVersion = new(1, 2, 1),
            CommitVersion = commitVersion,
        });
        await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var response = await tester.TestRequest<GetDeployClusterStateResponse, GetDeployClusterStateRequest>(new GetDeployClusterStateRequest(clusterId));

        // Assert
        response.RequestError.Should().BeNull();
        response.State.Should().NotBeNull();
        response.State.CurrentState.Should().Be("CommittingCluster");
        response.State.TargetVersion.Should().Be(commitVersion);
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
