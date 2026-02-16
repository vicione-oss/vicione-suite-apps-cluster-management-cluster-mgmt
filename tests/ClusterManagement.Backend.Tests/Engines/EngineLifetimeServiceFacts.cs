using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ClusterManagement.Backend.Engines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Instance;
using Sdk.Testing.Logging;

namespace ClusterManagement.Backend.Tests.Engines;

public sealed class EngineLifetimeService_Start_on_Master_and_slave
{
    public static TheoryData<ActualState, ExpectedState> UseCases() => new()
    {
        { new ActualState(HostState.None, ActualEngineState.None), new ExpectedState(HostState.None, ExpectedEngineState.None) },
        { new ActualState(HostState.Missing, ActualEngineState.None), new ExpectedState(HostState.Missing, ExpectedEngineState.None) },
        { new ActualState(HostState.Stopped, ActualEngineState.None), new ExpectedState(HostState.Stopped, ExpectedEngineState.None) },
        { new ActualState(HostState.Stopped, ActualEngineState.Missing), new ExpectedState(HostState.Running, ExpectedEngineState.Missing) },
        { new ActualState(HostState.Stopped, ActualEngineState.Missing, true), new ExpectedState(HostState.Running, ExpectedEngineState.Missing) },
        { new ActualState(HostState.Stopped, ActualEngineState.Stopped), new ExpectedState(HostState.Running, ExpectedEngineState.Stopped) },
        { new ActualState(HostState.Stopped, ActualEngineState.Stopped, true), new ExpectedState(HostState.Running, ExpectedEngineState.Running) },
        { new ActualState(HostState.Running, ActualEngineState.None), new ExpectedState(HostState.Running, ExpectedEngineState.None) },
        { new ActualState(HostState.Running, ActualEngineState.Missing), new ExpectedState(HostState.Running, ExpectedEngineState.Missing) },
        { new ActualState(HostState.Running, ActualEngineState.Missing, true), new ExpectedState(HostState.Running, ExpectedEngineState.Missing) },
        { new ActualState(HostState.Running, ActualEngineState.Stopped), new ExpectedState(HostState.Running, ExpectedEngineState.Stopped) },
        { new ActualState(HostState.Running, ActualEngineState.Stopped, true), new ExpectedState(HostState.Running, ExpectedEngineState.Running) },
        { new ActualState(HostState.Running, ActualEngineState.Running), new ExpectedState(HostState.Running, ExpectedEngineState.Running) },
        { new ActualState(HostState.Running, ActualEngineState.Running, true), new ExpectedState(HostState.Running, ExpectedEngineState.Running) },
    };

    [Theory]
    [Trait(Traits.Category, Traits.System)]
    [MemberData(nameof(UseCases))]
    public async Task Handles_different_states_correctly(ActualState actualState, ExpectedState expectedState)
    {
        await using var context = await ClusterSystemTestContext.Create(InstanceType.Master);
        var engineHost = Guid.NewGuid();
        var engine = Guid.NewGuid();
        await EngineLifetimeServiceEnvironment.Arrange(actualState, engineHost, engine, context);

        await context.When(AutoStartingEngines);

        context.Then_no_errors_were_logged();
        await EngineLifetimeServiceEnvironment.AssertHost(expectedState.Host, engineHost, context);
        switch (expectedState.Engine)
        {
            case ExpectedEngineState.None:
                await context.Then_no_engine_is_known();
                context.Then_no_engine_is_deployed();
                await context.Then_no_engine_is_running();
                context.Then_no_engine_was_started();
                break;
            case ExpectedEngineState.Missing:
                await context.Then_engine_is_known(engine, engineHost, actualState.ShouldRun);
                context.Then_no_engine_is_deployed();
                await context.Then_no_engine_is_running();
                context.Then_no_engine_was_started();
                break;
            case ExpectedEngineState.Stopped:
                await context.Then_engine_is_known(engine, engineHost, actualState.ShouldRun);
                await context.Then_engine_is_deployed(engine, engineHost);
                await context.Then_no_engine_is_running();
                context.Then_no_engine_was_started();
                break;
            case ExpectedEngineState.Running:
                await context.Then_engine_is_known(engine, engineHost, actualState.ShouldRun);
                await context.Then_engine_is_deployed(engine, engineHost);
                await context.Then_engine_is_running(engine, engineHost);
                if (actualState.Engine == ActualEngineState.Running)
                    context.Then_no_engine_was_started();
                else
                    context.Then_engine_was_started(engine);
                break;
        }
    }

    private static Task AutoStartingEngines(ClusterSystemTestContext context)
    {
        EngineLifetimeService engineLifetimeService = new(context.Services, Substitute.For<ILogger<EngineLifetimeService>>());
        return engineLifetimeService.StartedAsync(context.CancellationToken);
    }

    [Fact]
    [Trait(Traits.Category, Traits.System)]
    public async Task Starts_engine_hosts_and_engines_concurrently()
    {
        await using var context = await ClusterSystemTestContext.Create(InstanceType.Master);

        var engineHost1 = Guid.NewGuid();
        var engine1 = Guid.NewGuid();
        var engine2 = Guid.NewGuid();
        var engineHost2 = Guid.NewGuid();
        var engine3 = Guid.NewGuid();

        await context.Given_engineHost_is_known(engineHost1);
        await context.Given_engineHost_is_deployed(engineHost1);
        await context.Given_engineHost_is_running(engineHost1);
        await context.Given_engine_is_known(engine1, engineHost1, shouldRun: true);
        await context.Given_engine_is_deployed(engine1, engineHost1);
        await context.Given_engine_is_known(engine2, engineHost1, 1, shouldRun: true);
        await context.Given_engine_is_deployed(engine2, engineHost1);
        await context.Given_engineHost_is_not_running(engineHost1);

        await context.Given_engineHost_is_known(engineHost2);
        await context.Given_engineHost_is_deployed(engineHost2);
        await context.Given_engineHost_is_running(engineHost2);
        await context.Given_engine_is_known(engine3, engineHost2, shouldRun: true);
        await context.Given_engine_is_deployed(engine3, engineHost2);
        await context.Given_engineHost_is_not_running(engineHost2);

        await context.When(AutoStartingEngines);

        await context.Then_engineHost_is_known(engineHost1);
        context.Then_engineHost_is_deployed(engineHost1);
        context.Then_engineHost_is_running(engineHost1);
        await context.Then_engine_is_known(engine1, engineHost1, true);
        await context.Then_engine_is_deployed(engine1, engineHost1);
        await context.Then_engine_is_running(engine1, engineHost1);
        await context.Then_engine_is_known(engine2, engineHost1, true);
        await context.Then_engine_is_deployed(engine2, engineHost1);
        await context.Then_engine_is_running(engine2, engineHost1);

        await context.Then_engineHost_is_known(engineHost2);
        context.Then_engineHost_is_deployed(engineHost2);
        context.Then_engineHost_is_running(engineHost2);
        await context.Then_engine_is_known(engine3, engineHost2, true);
        await context.Then_engine_is_deployed(engine3, engineHost2);
        await context.Then_engine_is_running(engine3, engineHost2);
    }
}

public sealed class EngineLifetimeService_Start_on_Standalone
{
    [Fact]
    [Trait(Traits.Category, Traits.System)]
    public async Task Executes_deploy_cluster()
    {
        await using var context = await ClusterSystemTestContext.Create();

        await context.When(AutoStartingEngines);

        using var scope = context.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISuiteMediator>();
        await mediator.Received(1).Send(
            Arg.Is<Public.Commands.DeployCluster>(command => command.ClusterId == context.ClusterId && command.TargetVersion == context.ClusterVersion),
            context.CancellationToken);
    }

    private static Task AutoStartingEngines(ClusterSystemTestContext context)
    {
        EngineLifetimeService engineLifetimeService = new(context.Services, Substitute.For<ILogger<EngineLifetimeService>>());
        return engineLifetimeService.StartedAsync(context.CancellationToken);
    }
}

public sealed class EngineLifetimeService_Stop
{
    public static TheoryData<ActualState, ExpectedState> UseCases() => new()
    {
        { new ActualState(HostState.None, ActualEngineState.None), new ExpectedState(HostState.None, ExpectedEngineState.None) },
        { new ActualState(HostState.Missing, ActualEngineState.None), new ExpectedState(HostState.Missing, ExpectedEngineState.None) },
        { new ActualState(HostState.Stopped, ActualEngineState.None), new ExpectedState(HostState.Stopped, ExpectedEngineState.None) },
        { new ActualState(HostState.Stopped, ActualEngineState.Missing), new ExpectedState(HostState.Stopped, ExpectedEngineState.Missing) },
        { new ActualState(HostState.Stopped, ActualEngineState.Missing, true), new ExpectedState(HostState.Stopped, ExpectedEngineState.Missing) },
        { new ActualState(HostState.Stopped, ActualEngineState.Stopped), new ExpectedState(HostState.Stopped, ExpectedEngineState.Stopped) },
        { new ActualState(HostState.Stopped, ActualEngineState.Stopped, true), new ExpectedState(HostState.Stopped, ExpectedEngineState.Stopped) },
        { new ActualState(HostState.Running, ActualEngineState.None), new ExpectedState(HostState.Stopped, ExpectedEngineState.None) },
        { new ActualState(HostState.Running, ActualEngineState.Missing), new ExpectedState(HostState.Stopped, ExpectedEngineState.Missing) },
        { new ActualState(HostState.Running, ActualEngineState.Missing, true), new ExpectedState(HostState.Stopped, ExpectedEngineState.Missing) },
        { new ActualState(HostState.Running, ActualEngineState.Stopped), new ExpectedState(HostState.Stopped, ExpectedEngineState.Stopped) },
        { new ActualState(HostState.Running, ActualEngineState.Stopped, true), new ExpectedState(HostState.Stopped, ExpectedEngineState.Stopped) },
        { new ActualState(HostState.Running, ActualEngineState.Running), new ExpectedState(HostState.Stopped, ExpectedEngineState.Stopped) },
        { new ActualState(HostState.Running, ActualEngineState.Running, true), new ExpectedState(HostState.Stopped, ExpectedEngineState.Stopped) },
    };

    [Theory]
    [Trait(Traits.Category, Traits.System)]
    [MemberData(nameof(UseCases))]
    public async Task Handles_different_states_correctly(ActualState actualState, ExpectedState expectedState)
    {
        await using var context = await ClusterSystemTestContext.Create();
        var engineHost = Guid.NewGuid();
        var engine = Guid.NewGuid();
        await EngineLifetimeServiceEnvironment.Arrange(actualState, engineHost, engine, context);

        await context.When(AutoStoppingEngines);

        context.Then_no_errors_were_logged();
        await EngineLifetimeServiceEnvironment.AssertHost(expectedState.Host, engineHost, context);
        switch (expectedState.Engine)
        {
            case ExpectedEngineState.None:
                await context.Then_no_engine_is_known();
                context.Then_no_engine_is_deployed();
                await context.Then_no_engine_is_running();
                context.Then_no_engine_was_stopped();
                break;
            case ExpectedEngineState.Missing:
                await context.Then_engine_is_known(engine, engineHost, actualState.ShouldRun);
                context.Then_no_engine_is_deployed();
                await context.Then_no_engine_is_running();
                context.Then_engine_was_stopped(engine);
                break;
            case ExpectedEngineState.Stopped:
                await context.Then_engine_is_known(engine, engineHost, actualState.ShouldRun);
                await context.Then_engine_is_deployed(engine, engineHost);
                context.Then_engine_was_stopped(engine);
                break;
            default:
                expectedState.Engine.Should().NotBe(ExpectedEngineState.Running, "the code should stop engines");
                break;
        }
    }

    private static Task AutoStoppingEngines(ClusterSystemTestContext context)
    {
        EngineLifetimeService engineLifetimeService = new(context.Services, Substitute.For<ILogger<EngineLifetimeService>>());
        return engineLifetimeService.StoppingAsync(context.CancellationToken);
    }

    [Fact]
    [Trait(Traits.Category, Traits.System)]
    public async Task Stops_engine_hosts_and_engines_concurrently()
    {
        await using var context = await ClusterSystemTestContext.Create();

        var engineHost1 = Guid.NewGuid();
        var engine1 = Guid.NewGuid();
        var engine2 = Guid.NewGuid();
        var engineHost2 = Guid.NewGuid();
        var engine3 = Guid.NewGuid();

        await context.Given_engineHost_is_known(engineHost1);
        await context.Given_engineHost_is_deployed(engineHost1);
        await context.Given_engineHost_is_running(engineHost1);
        await context.Given_engine_is_known(engine1, engineHost1, shouldRun: true);
        await context.Given_engine_is_deployed(engine1, engineHost1);
        await context.Given_engine_is_running(engine1, engineHost1);
        await context.Given_engine_is_known(engine2, engineHost1, 1, shouldRun: true);
        await context.Given_engine_is_deployed(engine2, engineHost1);
        await context.Given_engine_is_running(engine2, engineHost1);

        await context.Given_engineHost_is_known(engineHost2);
        await context.Given_engineHost_is_deployed(engineHost2);
        await context.Given_engineHost_is_running(engineHost2);
        await context.Given_engine_is_known(engine3, engineHost2, shouldRun: true);
        await context.Given_engine_is_deployed(engine3, engineHost2);
        await context.Given_engine_is_running(engine3, engineHost2);

        await context.When(AutoStoppingEngines);

        await context.Then_engineHost_is_known(engineHost1);
        context.Then_engineHost_is_deployed(engineHost1);
        context.Then_engineHost_is_not_running(engineHost1);
        await context.Then_engine_is_known(engine1, engineHost1, true);
        await context.Then_engine_is_deployed(engine1, engineHost1);
        await context.Then_engine_is_known(engine2, engineHost1, true);
        await context.Then_engine_is_deployed(engine2, engineHost1);

        await context.Then_engineHost_is_known(engineHost2);
        context.Then_engineHost_is_deployed(engineHost2);
        context.Then_engineHost_is_not_running(engineHost2);
        await context.Then_engine_is_known(engine3, engineHost2, true);
        await context.Then_engine_is_deployed(engine3, engineHost2);
    }

    [Theory]
    [SupportedOSPlatform(nameof(OSPlatform.Linux))]
    [InlineData("os-pgk-upgrade")]
    [InlineData("suite-ui-restart")]
    public async Task Does_not_stop_when_signal_file_exists(string fileName)
    {
        ServiceCollection serviceCollection = new();
        var engineLifetime = Substitute.For<IEngineLifetime>();
        serviceCollection.AddTransient(_ => engineLifetime);
        var fileSystem = new MockFileSystem();
        serviceCollection.AddSingleton<IFileSystem>(fileSystem);
        await using var serviceProvider = serviceCollection.BuildServiceProvider();
        using TestLogger<EngineLifetimeService> logger = new();
        EngineLifetimeService engineLifetimeService = new(serviceProvider, logger);
        var runtimeDirectory = Environment.GetEnvironmentVariable("RUNTIME_DIRECTORY") ?? "/run/vicione-suite";
        var signalFile = fileSystem.Path.Combine(runtimeDirectory, fileName);
        fileSystem.AddEmptyFile(signalFile);

        await engineLifetimeService.StoppingAsync(CancellationToken.None);

        fileSystem.FileExists(signalFile).Should().BeFalse();
        engineLifetime.ReceivedCalls().Should().BeEmpty();
        logger.Calls.Should().Be(1);
    }
}

internal static class EngineLifetimeServiceEnvironment
{
    internal static async Task Arrange(ActualState actualState, Guid engineHost, Guid engine, ClusterSystemTestContext context)
    {
        switch (actualState.Host)
        {
            case HostState.None: break;
            case HostState.Missing:
                await context.Given_engineHost_is_known(engineHost);
                break;
            case HostState.Stopped:
                await context.Given_engineHost_is_known(engineHost);
                await context.Given_engineHost_is_deployed(engineHost);
                break;
            case HostState.Running:
                await context.Given_engineHost_is_known(engineHost);
                await context.Given_engineHost_is_deployed(engineHost);
                await context.Given_engineHost_is_running(engineHost);
                break;
        }

        switch (actualState.Engine)
        {
            case ActualEngineState.None: break;
            case ActualEngineState.Missing:
                await context.Given_engine_is_known(engine, engineHost, shouldRun: actualState.ShouldRun);
                break;
            case ActualEngineState.Stopped:

                await context.Given_engine_is_known(engine, engineHost, shouldRun: actualState.ShouldRun);
                if (actualState.Host == HostState.Stopped)
                    await context.Given_engineHost_is_running(engineHost);
                await context.Given_engine_is_deployed(engine, engineHost);
                if (actualState.Host == HostState.Stopped)
                    await context.Given_engineHost_is_not_running(engineHost);
                break;
            case ActualEngineState.Running:
                if (actualState.Host != HostState.Running)
                    throw new InvalidOperationException("Cannot run an engine on an non-running engine host.");
                await context.Given_engine_is_known(engine, engineHost, shouldRun: actualState.ShouldRun);
                await context.Given_engine_is_deployed(engine, engineHost);
                await context.Given_engine_is_running(engine, engineHost);
                break;
        }
    }

    internal static async Task AssertHost(HostState expectedState, Guid engineHost, ClusterSystemTestContext context)
    {
        switch (expectedState)
        {
            case HostState.None:
                await context.Then_no_engineHost_is_known();
                context.Then_no_engineHost_is_deployed();
                context.Then_engineHost_is_not_running(engineHost);
                break;
            case HostState.Missing:
                await context.Then_engineHost_is_known(engineHost);
                context.Then_no_engineHost_is_deployed();
                context.Then_engineHost_is_not_running(engineHost);
                break;
            case HostState.Stopped:
                await context.Then_engineHost_is_known(engineHost);
                context.Then_engineHost_is_deployed(engineHost);
                context.Then_engineHost_is_not_running(engineHost);
                break;
            case HostState.Running:
                await context.Then_engineHost_is_known(engineHost);
                context.Then_engineHost_is_deployed(engineHost);
                context.Then_engineHost_is_running(engineHost);
                break;
        }
    }
}

public sealed record class ActualState(HostState Host, ActualEngineState Engine, bool ShouldRun = false);

public sealed record class ExpectedState(HostState Host, ExpectedEngineState Engine);

public enum HostState
{
    None,
    Missing,
    Stopped,
    Running,
}

public enum ActualEngineState
{
    None,
    Missing,
    Stopped,
    Running,
}

public enum ExpectedEngineState
{
    None,
    Missing,
    Stopped,
    Running,
}
