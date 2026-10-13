using ClusterManagement.Backend.DeployCluster.Consumers;
using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Internal.Commands;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Testing.Backend;

namespace ClusterManagement.Backend.Tests.DeployCluster.Consumers;

public class ResetSagaStateConsumerTests : TestWithDbContextSqlite<ClusterSagaDbContextSqlite>
{
    private void SetupConsumer(IBusRegistrationConfigurator cfg)
    {
        cfg.AddConsumer<ResetSagaStateConsumer>();
        cfg.AddSingleton<IClusterSagaDbContext>(_ => TestDbContext);
        cfg.AddSingleton(Substitute.For<ILogger<ResetSagaStateConsumer>>());
    }

    [Fact]
    public async Task Should_remove_deploy_state_when_cluster_exists()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg));
        var clusterId = Guid.NewGuid();
        var state = new DeployClusterState { ClusterId = clusterId, CurrentState = 2 };
        TestDbContext.DeployClusterState.Add(state);
        await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new ResetSagaState(clusterId);

        // Act
        await tester.TestCommand<ResetSagaState, ResetSagaStateConsumer>(command);

        // Assert
        var remainingState = await TestDbContext.DeployClusterState
            .FirstOrDefaultAsync(k => k.ClusterId == clusterId, TestContext.Current.CancellationToken);
        remainingState.Should().BeNull();
    }

    [Fact]
    public async Task Should_remove_every_deploy_state_of_the_cluster_left_by_earlier_versions()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg));
        var clusterId = Guid.NewGuid();
        var otherClusterId = Guid.NewGuid();
        TestDbContext.DeployClusterState.AddRange(
            new DeployClusterState { CorrelationId = Guid.NewGuid(), ClusterId = clusterId, CurrentState = 5 },
            new DeployClusterState { CorrelationId = Guid.NewGuid(), ClusterId = clusterId, CurrentState = 7 },
            new DeployClusterState { CorrelationId = Guid.NewGuid(), ClusterId = otherClusterId, CurrentState = 5 });
        await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await tester.TestCommand<ResetSagaState, ResetSagaStateConsumer>(new ResetSagaState(clusterId));

        // Assert
        var remainingStates = await TestDbContext.DeployClusterState.ToListAsync(TestContext.Current.CancellationToken);
        remainingStates.Should().ContainSingle().Which.ClusterId.Should().Be(otherClusterId);
    }

    [Fact]
    public async Task Should_ignore_unknown_cluster_id()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg));
        var command = new ResetSagaState(Guid.NewGuid());

        // Act
        await tester.TestCommand<ResetSagaState, ResetSagaStateConsumer>(command);

        // Assert
        var remainingState = await TestDbContext.DeployClusterState.ToListAsync(TestContext.Current.CancellationToken);
        remainingState.Should().BeEmpty();
    }
}
