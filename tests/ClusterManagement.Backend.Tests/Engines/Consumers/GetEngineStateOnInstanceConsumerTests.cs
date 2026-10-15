using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Engines;
using ClusterManagement.Backend.Engines.Consumers;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.ErrorHandling;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Messaging;
using Sdk.Testing.Backend;

namespace ClusterManagement.Backend.Tests.Engines.Consumers;

public class GetEngineStateOnInstanceConsumerTests : TestWithDbContextSqlite<EngineDbContextSqlite>
{
    private readonly Action<IBusRegistrationConfigurator> _configureServices;
    private readonly IEngineHostCommunication _engineHostCommunication = Substitute.For<IEngineHostCommunication>();

    public GetEngineStateOnInstanceConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<GetEngineStateOnInstanceConsumer>();
            cfg.AddSingleton<IEngineDbContext>(_ => TestDbContext);
            cfg.AddSingleton(_engineHostCommunication);
        };

    public static TheoryData<Backend.Engines.EngineState> ReportedStates => [.. Enum.GetValues<Backend.Engines.EngineState>()];

    [Theory]
    [MemberData(nameof(ReportedStates))]
    public async Task Response_carries_every_state_the_engine_host_reports(Backend.Engines.EngineState reported)
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var engineHostId = Guid.NewGuid();
        var engineId = Guid.NewGuid();
        var deploymentId = Guid.NewGuid();
        TestDbContext.EngineHosts.Add(new EngineHost { Id = engineHostId });
        TestDbContext.EngineDeployments.Add(new EngineDeployment { Id = deploymentId, EngineHostId = engineHostId, EngineId = engineId });
        await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _engineHostCommunication.GetEngineState(engineHostId, deploymentId, Arg.Any<CancellationToken>()).Returns(reported);

        // Act
        var response = await tester.TestInstanceDependentRequest<GetEngineStateOnInstanceResponse, GetEngineStateOnInstanceRequest>(new GetEngineStateOnInstanceRequest(engineId));

        // Assert
        response.EngineState.Should().Be(Enum.Parse<Internal.Requests.EngineState>(reported.ToString()));
    }

    [Fact]
    public async Task Response_carries_the_state_and_the_deployment_id_of_the_engine()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var engineHostId = Guid.NewGuid();
        var engineId = Guid.NewGuid();
        var deploymentId = Guid.NewGuid();
        TestDbContext.EngineHosts.Add(new EngineHost { Id = engineHostId });
        TestDbContext.EngineDeployments.Add(new EngineDeployment { Id = deploymentId, EngineHostId = engineHostId, EngineId = engineId });
        await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _engineHostCommunication.GetEngineState(engineHostId, deploymentId, Arg.Any<CancellationToken>()).Returns(Backend.Engines.EngineState.Running);

        // Act
        var response = await tester.TestInstanceDependentRequest<GetEngineStateOnInstanceResponse, GetEngineStateOnInstanceRequest>(new GetEngineStateOnInstanceRequest(engineId));

        // Assert
        response.EngineState.Should().Be(Internal.Requests.EngineState.Running);
        response.DeploymentId.Should().Be(deploymentId);
    }

    [Fact]
    public async Task Response_carries_the_deployment_id_when_the_engine_host_does_not_answer()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var engineHostId = Guid.NewGuid();
        var engineId = Guid.NewGuid();
        var deploymentId = Guid.NewGuid();
        TestDbContext.EngineHosts.Add(new EngineHost { Id = engineHostId });
        TestDbContext.EngineDeployments.Add(new EngineDeployment { Id = deploymentId, EngineHostId = engineHostId, EngineId = engineId });
        await TestDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _engineHostCommunication.GetEngineState(engineHostId, deploymentId, Arg.Any<CancellationToken>())
            .Returns<Backend.Engines.EngineState>(_ => throw new TimeoutException("no answer"));

        // Act
        var response = await tester.TestInstanceDependentRequest<GetEngineStateOnInstanceResponse, GetEngineStateOnInstanceRequest>(new GetEngineStateOnInstanceRequest(engineId));

        // Assert
        response.RequestError!.ErrorCode.Should().Be(ClusterErrors.EngineHostDidNotAnswer);
        response.DeploymentId.Should().Be(deploymentId);
    }

    [Fact]
    public async Task Response_says_so_when_the_engine_is_not_deployed_on_the_instance()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);

        // Act
        var response = await tester.TestInstanceDependentRequest<GetEngineStateOnInstanceResponse, GetEngineStateOnInstanceRequest>(new GetEngineStateOnInstanceRequest(Guid.NewGuid()));

        // Assert
        response.RequestError!.ErrorCode.Should().Be(ClusterErrors.EngineNotDeployed);
        response.DeploymentId.Should().BeNull();
    }

    [Fact]
    public async Task Response_says_so_when_asking_fails_unexpectedly()
    {
        // Arrange
        var consumer = new GetEngineStateOnInstanceConsumer(_engineHostCommunication, TestDbContext);

        // Act
        var response = await consumer.HandleException(new GetEngineStateOnInstanceRequest(Guid.NewGuid()),
            new InvalidOperationException("Database is locked"), TestContext.Current.CancellationToken);

        // Assert
        response.RequestError.Should().Be(new ErrorInfo(ClusterError.UnknownError, "Database is locked"));
    }
}
