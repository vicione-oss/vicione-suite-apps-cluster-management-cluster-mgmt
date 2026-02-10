using AwesomeAssertions;
using ClusterManagement.Backend.DeployCluster.Consumers;
using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Public.Iodds.Commands;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Testing.Backend;
using Xunit;

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
        await TestDbContext.SaveChangesAsync();

        var command = new ResetSagaState(clusterId);

        // Act
        await tester.TestCommand<ResetSagaState, ResetSagaStateConsumer>(command);

        // Assert
        var remainingState = await TestDbContext.DeployClusterState
            .FirstOrDefaultAsync(k => k.ClusterId == clusterId);
        remainingState.Should().BeNull();
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
        var remainingState = await TestDbContext.DeployClusterState.ToListAsync();
        remainingState.Should().BeEmpty();
    }
}
