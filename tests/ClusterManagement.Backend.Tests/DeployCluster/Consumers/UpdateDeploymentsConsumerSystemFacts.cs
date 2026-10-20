using System.IO.Abstractions;
using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.DeployCluster.Consumers;
using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Backend.EngineHosts;
using ClusterManagement.Backend.Mqtt;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Public.Designs;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using Sdk.Instance;
using static ClusterManagement.Backend.Tests.DeployCluster.Consumers.UpdateDeploymentsConsumerFacts;

namespace ClusterManagement.Backend.Tests.DeployCluster.Consumers;

public class UpdateDeploymentsConsumerSystemFacts
{
    private readonly Guid _engineHost = Guid.NewGuid();
    private readonly Guid _engine = Guid.NewGuid();
    private readonly Guid _functionBlock = Guid.NewGuid();

    [Theory]
    [Trait(Traits.Category, Traits.System)]
    [MemberData(nameof(UseCases), MemberType = typeof(UpdateDeploymentsConsumerFacts))]
    public async Task Handles_different_states_correctly(ClusterState cluster, EngineDbState engineDbContext, PhysicalState physical, ExpectedState expected,
        EngineHostActions _, EngineActions engine)
    {
        await using var context = await ClusterSystemTestContext.Create();

        await Arrange(cluster, engineDbContext, physical, context);

        var result = await context.When(UpdatingDeployments);

        await Assert(result, cluster, expected, engine, context);
    }

    private async Task Arrange(ClusterState cluster, EngineDbState engineDbContext, PhysicalState physical, ClusterSystemTestContext context)
    {
        if (cluster.Application == Existence.Defined)
            context.Given_user_added_cluster_application();
        if (cluster.Host == Existence.Defined)
            context.Given_user_added_engineHost(_engineHost);
        if (cluster.Engine != Activation.None)
        {
            context.Given_user_added_engine(_engine, _engineHost);
            if (cluster.Engine == Activation.Disabled)
                context.Given_user_disabled_engine(_engine);
        }
        if (cluster.FunctionBlock == Existence.Defined)
            context.Given_user_added_FunctionBlock(_functionBlock, _engine);

        if (engineDbContext.Host == Existence.Defined)
            await context.Given_engineHost_is_known(_engineHost);
        if (engineDbContext.Engine != Activation.None)
            await context.Given_engine_is_known(_engine, _engineHost, shouldRun: engineDbContext.Engine == Activation.Enabled);
        if (engineDbContext.FunctionBlock == Existence.Defined)
            await context.Given_engine_has_functionBlock(_engine, _engineHost, _functionBlock);

        switch (physical.Host)
        {
            case Stoppable.Stopped:
                await context.Given_engineHost_is_deployed(_engineHost);
                break;
            case Stoppable.Running:
                await context.Given_engineHost_is_deployed(_engineHost);
                await context.Given_engineHost_is_running(_engineHost);
                break;
        }

        switch (physical.Engine)
        {
            case Stoppable.Stopped:
                if (physical.Host == Stoppable.None)
                    throw new InvalidOperationException("Cannot deploy an engine on a non-existing engine host.");
                if (physical.Host == Stoppable.Stopped)
                    await context.Given_engineHost_is_running(_engineHost);
                await context.Given_engine_is_deployed(_engine, _engineHost);
                if (physical.Host == Stoppable.Stopped)
                    await context.Given_engineHost_is_not_running(_engineHost);
                break;
            case Stoppable.Running:
                if (physical.Host != Stoppable.Running)
                    throw new InvalidOperationException("Cannot run an engine on an non-running engine host.");
                await context.Given_engine_is_deployed(_engine, _engineHost);
                await context.Given_engine_is_running(_engine, _engineHost);
                break;
        }
    }

    private static async Task<CorrelatedBy<Guid>?> UpdatingDeployments(ClusterSystemTestContext context)
    {
        var consumeContext = Substitute.For<ConsumeContext<UpdateDeployments>>();

        var store = context.Services.GetRequiredService<IClusterSnapshotStore>();
        var clusterJson = await store.LoadCompressedJson(context.ClusterId, context.ClusterVersion, context.CancellationToken);
        if (clusterJson.Length == 0)
            throw new InvalidOperationException("Cant load cluster from store!");
        consumeContext.Message.Returns(new UpdateDeployments(clusterJson, context.CorrelationId, context.ClusterId, context.ClusterVersion));
        consumeContext.CorrelationId.Returns(context.CorrelationId);
        consumeContext.CancellationToken.Returns(context.CancellationToken);
        consumeContext.When(x => x.Publish(Arg.Any<Arg.AnyType>(), Arg.Is(context.CancellationToken))).Do(x => context.AddEvent(x[0]));

        using var scope = context.Services.CreateScope();
        UpdateDeploymentsConsumer consumer = new(
            scope.ServiceProvider.GetRequiredService<IInstanceInformationProvider>(),
            scope.ServiceProvider.GetRequiredService<IEngineDbContext>(),
            scope.ServiceProvider.GetRequiredService<IEngineHostPoolCommunication>(),
            scope.ServiceProvider.GetRequiredService<IEngineHostCommunication>(),
            scope.ServiceProvider.GetRequiredService<IMqttBrokerConnectionCache>(),
            scope.ServiceProvider.GetRequiredService<IDesignProvider>(),
            scope.ServiceProvider.GetRequiredService<IClusterDbContext>(),
            scope.ServiceProvider.GetRequiredService<IWorkspaceProvider<ClusterManagementBackendModule>>(),
            scope.ServiceProvider.GetRequiredService<ISuiteRulesetProvider>(),
            scope.ServiceProvider.GetRequiredService<IFileSystem>(),
            Substitute.For<IBus>(),
            scope.ServiceProvider.GetRequiredService<ILogger<UpdateDeploymentsConsumer>>());
        await consumer.Consume(consumeContext);

        return context.Events.OfType<CorrelatedBy<Guid>>().SingleOrDefault();
    }

    private async Task Assert(CorrelatedBy<Guid>? result, ClusterState clusterState, ExpectedState expected,
        EngineActions engine, ClusterSystemTestContext context)
    {
        context.Then(UpdateSucceeded, result);

        switch (clusterState.Application)
        {
            case Existence.None: context.Then_no_cluster_application_exists(); break;
            case Existence.Defined: context.Then_cluster_application_exists(); break;
        }

        if (clusterState.Host == Existence.Defined && clusterState.Engine != Activation.None && clusterState.FunctionBlock != Existence.None)
            await context.Then_engineHost_is_known(_engineHost);
        else
            await context.Then_no_engineHost_is_known();

        switch (clusterState.Engine)
        {
            case Activation.Disabled when clusterState.FunctionBlock != Existence.None: await context.Then_engine_is_known(_engine, _engineHost, false); break;
            case Activation.Enabled when clusterState.FunctionBlock != Existence.None: await context.Then_engine_is_known(_engine, _engineHost, true); break;
            default: await context.Then_no_engine_is_known(); break;
        }

        switch (expected.Host)
        {
            case Runnable.None:
                context.Then_no_engineHost_is_deployed();
                context.Then_engineHost_is_not_running(_engineHost);
                break;
            case Runnable.Running:
                context.Then_engineHost_is_deployed(_engineHost);
                context.Then_engineHost_is_running(_engineHost);
                break;
        }

        switch (expected.Engine)
        {
            case Stoppable.None:
                context.Then_no_engine_is_deployed();
                await context.Then_no_engine_is_running();
                break;
            case Stoppable.Stopped:
                await context.Then_engine_is_deployed(_engine, _engineHost);
                await context.Then_no_engine_is_running();
                break;
            case Stoppable.Running:
                await context.Then_engine_is_deployed(_engine, _engineHost);
                await context.Then_engine_is_running(_engine, _engineHost);
                break;
        }

        if (engine.Started)
            context.Then_engine_was_started(_engine);
        else
            context.Then_no_engine_was_started();
        if (engine.Stopped)
            context.Then_engine_was_stopped(_engine);
        else
            context.Then_no_engine_was_stopped();
        if (engine.TornDown)
            context.Then_engine_was_torn_down(_engine);
        else
            context.Then_no_engine_was_torn_down();
    }

    private static void UpdateSucceeded(ClusterSystemTestContext context, CorrelatedBy<Guid>? result)
    {
        var updatedEvent = result.Should().NotBeNull().And.BeOfType<ClusterNodeDeploymentsUpdatedEvent>().Subject;
        updatedEvent.CorrelationId.Should().Be(context.CorrelationId);
        updatedEvent.ClusterVersion.Should().Be(context.ClusterVersion);
        context.Then_no_errors_were_logged();
    }

    [Fact]
    [Trait(Traits.Category, Traits.System)]
    public async Task Can_adjust_the_engine_chain_cycle_time_for_several_engines_on_same_host()
    {
        var otherEngine = Guid.NewGuid();
        var otherFunctionBlock = Guid.NewGuid();
        await using var context = await ClusterSystemTestContext.Create();

        context.Given_user_added_cluster_application();
        context.Given_user_added_engineHost(_engineHost);
        context.Given_user_added_engine(_engine, _engineHost, 0, 200);
        context.Given_user_added_FunctionBlock(_functionBlock, _engine);
        context.Given_user_added_engine(otherEngine, _engineHost, 1, 200);
        context.Given_user_added_FunctionBlock(otherFunctionBlock, otherEngine);
        await context.Given_engineHost_is_known(_engineHost);
        await context.Given_engineHost_is_deployed(_engineHost);
        await context.Given_engineHost_is_running(_engineHost);
        await context.Given_engine_is_known(_engine, _engineHost);
        await context.Given_engine_is_deployed(_engine, _engineHost);
        await context.Given_engine_is_running(_engine, _engineHost);
        await context.Given_engine_is_known(otherEngine, _engineHost, 1);
        await context.Given_engine_is_deployed(otherEngine, _engineHost);
        await context.Given_engine_is_running(otherEngine, _engineHost);

        var result = await context.When(UpdatingDeployments);

        context.Then(UpdateSucceeded, result);
        await context.Then_engineHost_is_known(_engineHost);
        context.Then_engineHost_is_deployed(_engineHost);
        context.Then_engineHost_is_running(_engineHost);
        await context.Then_engine_is_known(_engine, _engineHost, true);
        await context.Then_engine_is_deployed(_engine, _engineHost);
        await context.Then_engine_is_running(_engine, _engineHost);
        await context.Then_engine_is_known(otherEngine, _engineHost, true);
        await context.Then_engine_is_deployed(otherEngine, _engineHost);
        await context.Then_engine_is_running(otherEngine, _engineHost);
        context.Then_engine_was_stopped(_engine);
        context.Then_engine_was_stopped(otherEngine);
        context.Then_engine_was_torn_down(_engine);
        context.Then_engine_was_torn_down(otherEngine);
        context.Then_engine_was_started(_engine);
        context.Then_engine_was_started(otherEngine);
    }

    [Fact]
    [Trait(Traits.Category, Traits.System)]
    public async Task Restarts_running_engines_of_running_engine_host_after_appsettings_update()
    {
        await using var context = await ClusterSystemTestContext.Create();

        context.Given_user_added_cluster_application();
        context.Given_user_added_engineHost(_engineHost);
        context.Given_user_added_engine(_engine, _engineHost);
        context.Given_user_added_FunctionBlock(_functionBlock, _engine);
        await context.Given_engineHost_is_known(_engineHost);
        await context.Given_engineHost_is_deployed(_engineHost, LogLevel.Debug);
        await context.Given_engineHost_is_running(_engineHost);
        await context.Given_engine_is_known(_engine, _engineHost, shouldRun: true);
        await context.Given_engine_has_functionBlock(_engine, _engineHost, _functionBlock);
        await context.Given_engine_is_deployed(_engine, _engineHost);
        await context.Given_engine_is_running(_engine, _engineHost);

        var result = await context.When(UpdatingDeployments);

        context.Then(UpdateSucceeded, result);
        await context.Then_engineHost_is_known(_engineHost);
        context.Then_engineHost_is_deployed(_engineHost);
        context.Then_engineHost_is_running(_engineHost);
        await context.Then_engine_is_known(_engine, _engineHost, true);
        await context.Then_engine_is_deployed(_engine, _engineHost);
        await context.Then_engine_is_running(_engine, _engineHost);
        context.Then_no_engine_was_stopped();
        context.Then_no_engine_was_torn_down();
        context.Then_engine_was_started(_engine);
    }

    [Fact]
    [Trait(Traits.Category, Traits.System)]
    public async Task Restarts_running_engines_of_running_engine_host_after_elevated_privileges_update()
    {
        await using var context = await ClusterSystemTestContext.Create();

        context.Given_user_added_cluster_application();
        context.Given_user_added_engineHost(_engineHost);
        context.Given_user_added_engine(_engine, _engineHost);
        context.Given_user_added_FunctionBlock(_functionBlock, _engine);
        await context.Given_engineHost_is_known(_engineHost, true);
        await context.Given_engineHost_is_deployed(_engineHost);
        await context.Given_engineHost_is_running(_engineHost);
        await context.Given_engine_is_known(_engine, _engineHost, shouldRun: true);
        await context.Given_engine_has_functionBlock(_engine, _engineHost, _functionBlock);
        await context.Given_engine_is_deployed(_engine, _engineHost);
        await context.Given_engine_is_running(_engine, _engineHost);

        var result = await context.When(UpdatingDeployments);

        context.Then(UpdateSucceeded, result);
        await context.Then_engineHost_is_known(_engineHost);
        context.Then_engineHost_is_deployed(_engineHost);
        context.Then_engineHost_is_running(_engineHost);
        await context.Then_engine_is_known(_engine, _engineHost, true);
        await context.Then_engine_is_deployed(_engine, _engineHost);
        await context.Then_engine_is_running(_engine, _engineHost);
        context.Then_no_engine_was_stopped();
        context.Then_no_engine_was_torn_down();
        context.Then_engine_was_started(_engine);
    }
}
