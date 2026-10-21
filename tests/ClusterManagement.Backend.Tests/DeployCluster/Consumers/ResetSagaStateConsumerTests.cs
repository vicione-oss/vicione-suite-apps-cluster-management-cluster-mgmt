using ClusterManagement.Backend.DeployCluster.Consumers;
using ClusterManagement.Backend.DeployCluster.Persistence;
using ClusterManagement.Backend.DeployCluster.StateMachines;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Events;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Testing.Backend;

namespace ClusterManagement.Backend.Tests.DeployCluster.Consumers;

public class ResetSagaStateConsumerTests : TestWithDbContextSqlite<ClusterSagaDbContextSqlite>
{
    // RollingBackCommit in the persisted state names of the state machine.
    private const int RollingBackCommit = 9;

    private static readonly Guid s_clusterId = Guid.NewGuid();

    private readonly ClusterLock _clusterLock = new();

    private void SetupConsumer(IBusRegistrationConfigurator cfg)
    {
        cfg.AddConsumer<ResetSagaStateConsumer>();
        cfg.AddSingleton<IClusterSagaDbContext>(_ => TestDbContext);
        cfg.AddSingleton<IClusterLock>(_clusterLock);
        cfg.AddSingleton(Substitute.For<ILogger<ResetSagaStateConsumer>>());
    }

    [Fact]
    public void Has_an_endpoint_of_its_own_so_a_blocked_command_endpoint_does_not_hold_it_back()
        => MessagingHelper.GetCommandEndpointName(typeof(ResetSagaStateConsumer), typeof(ResetSagaState), null)
            .Should().Be(nameof(ResetSagaState));

    [Fact]
    public async Task Removes_every_deployment_state_of_the_cluster()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg));
        var otherCluster = await AddState(Guid.NewGuid(), Guid.NewGuid());
        await AddState(s_clusterId, Guid.NewGuid());
        await AddState(s_clusterId, Guid.NewGuid());

        // Act
        await tester.TestCommand<ResetSagaState, ResetSagaStateConsumer>(Command(s_clusterId));

        // Assert
        var remaining = await TestDbContext.DeployClusterState.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
        remaining.Should().ContainSingle().Which.CorrelationId.Should().Be(otherCluster.CorrelationId);
    }

    [Fact]
    public async Task Tells_the_deployments_it_reset()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg));
        var state = await AddState(s_clusterId, Guid.NewGuid());
        var command = Command(s_clusterId);

        // Act
        await tester.TestCommand<ResetSagaState, ResetSagaStateConsumer>(command);

        // Assert
        var reset = await Outcome(tester, command);
        reset.Error.Should().BeNull();
        reset.ClusterId.Should().Be(s_clusterId);
        var deployment = reset.Reset.Should().ContainSingle().Subject;
        deployment.CorrelationId.Should().Be(state.CorrelationId);
        deployment.CurrentState.Should().Be(nameof(DeployClusterStateMachine.RollingBackCommit));
        deployment.ActiveVersion.Should().Be(new Version(0, 1, 4));
        deployment.TargetVersion.Should().Be(new Version(0, 1, 6));
    }

    [Fact]
    public async Task Ends_a_reset_deployment_as_failed_so_nobody_waits_for_it()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg));
        var state = await AddState(s_clusterId, Guid.NewGuid());

        // Act
        await tester.TestCommand<ResetSagaState, ResetSagaStateConsumer>(Command(s_clusterId));

        // Assert
        (await tester.Harness.Published.Any<ClusterUpdateFailed>(m =>
            m.Context.Message.CorrelationId == state.CorrelationId
            && m.Context.Message.ClusterId == s_clusterId
            && m.Context.Message.Version == new Version(0, 1, 6)
            && m.Context.Message.State == nameof(DeployClusterStateMachine.RollingBackCommit)
            && m.Context.Message.Reason == ResetSagaState.FailureReason
            && m.Context.Message.WasReset,
            TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    // Issue #380: the commit version of a reset deployment is free for the next commit
    [Fact]
    public async Task Releases_the_commit_version_a_reset_deployment_reserved()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg));
        var state = await AddState(s_clusterId, Guid.NewGuid());
        _clusterLock.Reserve(s_clusterId, state.CommitVersion);

        // Act
        await tester.TestCommand<ResetSagaState, ResetSagaStateConsumer>(Command(s_clusterId));

        // Assert
        _clusterLock.GetReservedVersions(s_clusterId).Should().BeEmpty();
    }

    [Theory]
    [InlineData("0.1.6", "0.0.0", "0.1.6")]
    [InlineData("0.1.6", "0.1.7", "0.1.7")]
    public async Task Names_a_deployment_by_the_version_the_saga_fails_it_with(string target, string commit, string expected)
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg));
        var state = await AddState(s_clusterId, Guid.NewGuid());
        state.TargetVersion = Version.Parse(target);
        state.CommitVersion = Version.Parse(commit);
        await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var command = Command(s_clusterId);

        // Act
        await tester.TestCommand<ResetSagaState, ResetSagaStateConsumer>(command);

        // Assert
        (await Outcome(tester, command)).Reset.Should().ContainSingle().Which.TargetVersion.Should().Be(Version.Parse(expected));
        (await tester.Harness.Published.Any<ClusterUpdateFailed>(m => m.Context.Message.Version == Version.Parse(expected),
            TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task Tells_that_nothing_was_reset_for_a_cluster_without_a_deployment()
    {
        // Arrange
        await using var tester = new MassTransitTester(cfg => SetupConsumer(cfg));
        var command = Command(Guid.NewGuid());

        // Act
        await tester.TestCommand<ResetSagaState, ResetSagaStateConsumer>(command);

        // Assert
        var reset = await Outcome(tester, command);
        reset.Error.Should().BeNull();
        reset.Reset.Should().BeEmpty();
        (await tester.Harness.Published.Any<ClusterUpdateFailed>(TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Tells_why_the_reset_failed_once_every_attempt_failed()
    {
        // Arrange
        var command = Command(s_clusterId);
        var fault = Substitute.For<Fault<ResetSagaState>>();
        fault.Message.Returns(command);
        var exception = Substitute.For<ExceptionInfo>();
        exception.Message.Returns("Database is locked");
        fault.Exceptions.Returns([exception]);
        var context = Substitute.For<ConsumeContext<Fault<ResetSagaState>>>();
        context.Message.Returns(fault);
        var consumer = new ResetSagaStateConsumer(TestDbContext, _clusterLock, Substitute.For<ILogger<ResetSagaStateConsumer>>());

        // Act
        await consumer.Consume(context);

        // Assert
        await context.Received(1).Publish(Arg.Is<DeploymentStateReset>(reset =>
            reset.CorrelationId == command.CorrelationId
            && reset.Reset.Count == 0
            && reset.Error!.Message == "Database is locked"), Arg.Any<CancellationToken>());
    }

    private static ResetSagaState Command(Guid clusterId)
        => new(clusterId, "admin");

    private static async Task<DeploymentStateReset> Outcome(MassTransitTester tester, ResetSagaState command)
    {
        (await tester.Harness.Published.Any<DeploymentStateReset>(m => m.Context.Message.CorrelationId == command.CorrelationId,
            TestContext.Current.CancellationToken)).Should().BeTrue();

        return tester.Harness.Published.Select<DeploymentStateReset>(TestContext.Current.CancellationToken)
            .Single(m => m.Context.Message.CorrelationId == command.CorrelationId).Context.Message;
    }

    // A deployment that committed v0.1.6 and hangs while removing it again.
    private async Task<DeployClusterState> AddState(Guid clusterId, Guid correlationId)
    {
        var state = new DeployClusterState
        {
            CorrelationId = correlationId,
            ClusterId = clusterId,
            CurrentState = RollingBackCommit,
            ActiveVersion = new Version(0, 1, 4),
            TargetVersion = new Version(0, 1, 6),
            CommitVersion = new Version(0, 1, 6),
            Started = DateTime.UtcNow,
        };
        TestDbContext.DeployClusterState.Add(state);
        await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return state;
    }
}
