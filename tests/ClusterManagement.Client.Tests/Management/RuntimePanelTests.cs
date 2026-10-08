using System.Collections.Concurrent;
using System.Globalization;
using Bunit;
using ClusterManagement.Client.Management.Components;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.ErrorHandling;
using ClusterManagement.Public.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Connections.Contracts;
using Sdk.Instance;
using Sdk.Messaging;
using Sdk.Testing.Client;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Tests.Management;

public class RuntimePanelTests
{
    private readonly Guid _instanceId = Guid.NewGuid();
    private readonly Guid _otherInstanceId = Guid.NewGuid();
    private readonly Engine _engine = new() { Id = Guid.NewGuid(), Name = "DCW-Device-Scanner", MinCycleTime = 500 };
    private readonly Engine _otherEngine = new() { Id = Guid.NewGuid(), Name = "Line-Monitor", MinCycleTime = 500 };
    private readonly MQTTnet.IMqttClient _client = Substitute.For<MQTTnet.IMqttClient>();
    private IUiMediator _mediator = default!;

    [Fact]
    public async Task Shows_the_state_of_every_engine()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        StateOf(_instanceId).Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running, DeploymentId = Guid.NewGuid() });

        // Act
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, Cluster((_instanceId, _engine))));

        // Assert
        sut.WaitForAssertion(() =>
        {
            sut.Find(".engine .state").TextContent.Should().Be(ClusterOverview.EngineRunning);
            sut.Find(".head-info .chip").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.EnginesRunning, 1, 1));
        });
    }

    [Fact]
    public async Task Marks_an_engine_whose_host_does_not_answer_as_not_reachable()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        StateOf(_instanceId).Returns<GetEngineStateOnInstanceResponse>(_ => throw new TimeoutException());

        // Act
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, Cluster((_instanceId, _engine))));

        // Assert
        sut.WaitForAssertion(() => sut.Find(".engine .state").TextContent.Should().Be(ClusterOverview.EngineNotReachable));
    }

    [Fact]
    public async Task Shows_an_engine_without_a_deployment_on_its_instance_as_not_deployed_and_does_not_ask_again()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        ctx.Services.AddKeyedSingleton<TimeProvider>(Sdk.Constants.ClientTimeProviderServiceKey, new ImmediateTimeProvider());
        var attempts = 0;
        StateOf(_instanceId).Returns(_ =>
        {
            attempts++;
            return new GetEngineStateOnInstanceResponse(new ErrorInfo(ClusterErrors.EngineNotDeployed, "Cannot find engine"));
        });

        // Act
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, Cluster((_instanceId, _engine))));

        // Assert
        sut.WaitForAssertion(() => sut.Find(".engine .state").TextContent.Should().Be(ClusterOverview.EngineNotDeployed));
        await Task.Delay(200, Xunit.TestContext.Current.CancellationToken);
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task Asks_again_once_however_many_events_arrive_while_the_engine_host_does_not_answer()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        var timers = new ManualTimersTimeProvider();
        ctx.Services.AddKeyedSingleton<TimeProvider>(Sdk.Constants.ClientTimeProviderServiceKey, timers);
        StateOf(_instanceId).Returns<GetEngineStateOnInstanceResponse>(_ => throw new TimeoutException());
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, Cluster((_instanceId, _engine))));

        // Act
        for (var i = 0; i < 3; i++)
            await sut.InvokeAsync(() => sut.Instance.Consume(new ClientContext<EngineStopped>(new EngineStopped(_engine.Id), Guid.NewGuid()), CancellationToken.None));

        // Assert
        timers.Count(TimeSpan.FromSeconds(10)).Should().Be(1);
    }

    [Fact]
    public async Task Keeps_the_newer_state_when_an_older_answer_arrives_later()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        var firstAnswer = new TaskCompletionSource<GetEngineStateOnInstanceResponse>();
        var queries = 0;
        StateOf(_instanceId).Returns(_ => ++queries == 1
            ? firstAnswer.Task
            : Task.FromResult(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running }));
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, Cluster((_instanceId, _engine))));
        await sut.InvokeAsync(() => sut.Instance.Consume(new ClientContext<EngineStarted>(new EngineStarted(_engine.Id), Guid.NewGuid()), CancellationToken.None));
        sut.WaitForAssertion(() => sut.Find(".engine .state").TextContent.Should().Be(ClusterOverview.EngineRunning));

        // Act
        firstAnswer.SetException(new TimeoutException());
        await Task.Delay(300, Xunit.TestContext.Current.CancellationToken);

        // Assert
        sut.Find(".engine .state").TextContent.Should().Be(ClusterOverview.EngineRunning);
    }

    [Fact]
    public async Task Stops_waiting_for_an_instance_that_never_answers()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        ctx.Services.AddKeyedSingleton<TimeProvider>(Sdk.Constants.ClientTimeProviderServiceKey, new ImmediateTimeProvider());
        StateOf(_instanceId).Returns(new TaskCompletionSource<GetEngineStateOnInstanceResponse>().Task);

        // Act
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, Cluster((_instanceId, _engine))));

        // Assert
        sut.WaitForAssertion(() =>
        {
            sut.Find(".engine .state").TextContent.Should().Be(ClusterOverview.EngineNotReachable);
            sut.Find(".head-info .checked").TextContent.Should().NotBe("…");
        });
    }

    [Fact]
    public async Task Asks_an_unreachable_engine_again_later()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        ctx.Services.AddKeyedSingleton<TimeProvider>(Sdk.Constants.ClientTimeProviderServiceKey, new ImmediateTimeProvider());
        var attempts = 0;
        StateOf(_instanceId).Returns(_ => ++attempts == 1
            ? Task.FromException<GetEngineStateOnInstanceResponse>(new TimeoutException())
            : Task.FromResult(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running }));

        // Act
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, Cluster((_instanceId, _engine))));

        // Assert
        sut.WaitForAssertion(() => sut.Find(".engine .state").TextContent.Should().Be(ClusterOverview.EngineRunning));
        attempts.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task Warns_once_while_an_engine_host_stays_unreachable()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        ctx.Services.AddKeyedSingleton<TimeProvider>(Sdk.Constants.ClientTimeProviderServiceKey, new ImmediateTimeProvider());
        var logger = AddLogger(ctx);
        var attempts = 0;
        StateOf(_instanceId).Returns(_ => ++attempts <= 3
            ? Task.FromException<GetEngineStateOnInstanceResponse>(new TimeoutException())
            : Task.FromResult(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running }));

        // Act
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, Cluster((_instanceId, _engine))));

        // Assert
        sut.WaitForAssertion(() => sut.Find(".engine .state").TextContent.Should().Be(ClusterOverview.EngineRunning));
        LogLevels(logger).Should().Equal(LogLevel.Warning, LogLevel.Debug, LogLevel.Debug);
    }

    [Fact]
    public async Task Warns_when_the_host_of_an_engine_reported_as_not_deployed_stops_answering()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        var logger = AddLogger(ctx);
        var attempts = 0;
        StateOf(_instanceId).Returns(_ => ++attempts == 1
            ? Task.FromResult(new GetEngineStateOnInstanceResponse(new ErrorInfo(ClusterErrors.EngineNotDeployed, "Cannot find engine")))
            : Task.FromException<GetEngineStateOnInstanceResponse>(new TimeoutException()));
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, Cluster((_instanceId, _engine))));
        sut.WaitForAssertion(() => sut.Find(".engine .state").TextContent.Should().Be(ClusterOverview.EngineNotDeployed));

        // Act
        await sut.InvokeAsync(() => sut.Instance.Consume(new ClientContext<EngineStarted>(new EngineStarted(_engine.Id), Guid.NewGuid()), CancellationToken.None));

        // Assert
        sut.WaitForAssertion(() => LogLevels(logger).Should().Equal(LogLevel.Warning));
    }

    [Fact]
    public async Task Shows_each_engine_state_as_soon_as_its_instance_answers()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        StateOf(_instanceId).Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running });
        StateOf(_otherInstanceId).Returns(new TaskCompletionSource<GetEngineStateOnInstanceResponse>().Task);

        // Act
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, Cluster((_instanceId, _engine), (_otherInstanceId, _otherEngine))));

        // Assert
        sut.WaitForAssertion(() => sut.FindAll(".engine .state").Select(s => s.TextContent).Should().Equal(ClusterOverview.EngineRunning, "…"));
    }

    [Fact]
    public async Task An_engine_event_reloads_only_that_engine()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        StateOf(_instanceId).Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running });
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, Cluster((_instanceId, _engine), (_instanceId, _otherEngine))));
        sut.WaitForAssertion(() => sut.FindAll(".engine .state").Select(s => s.TextContent).Should().AllBe(ClusterOverview.EngineRunning));
        _mediator.ClearReceivedCalls();

        // Act
        await sut.InvokeAsync(() => sut.Instance.Consume(new ClientContext<EngineStopped>(new EngineStopped(_otherEngine.Id), Guid.NewGuid()), CancellationToken.None));

        // Assert
        await _mediator.Received(1).Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(
            Arg.Is<GetEngineStateOnInstanceRequest>(r => r.EngineId == _otherEngine.Id), _instanceId, Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(
            Arg.Is<GetEngineStateOnInstanceRequest>(r => r.EngineId == _engine.Id), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handles_an_engine_event_without_waiting_for_the_state_of_the_engine()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        ctx.Services.AddKeyedSingleton<TimeProvider>(Sdk.Constants.ClientTimeProviderServiceKey, new StoppedTimersTimeProvider());
        var queries = 0;
        StateOf(_instanceId).Returns(_ =>
        {
            queries++;
            return new TaskCompletionSource<GetEngineStateOnInstanceResponse>().Task;
        });
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, Cluster((_instanceId, _engine))));

        // Act
        await sut.InvokeAsync(() => sut.Instance.Consume(new ClientContext<EngineStopped>(new EngineStopped(_engine.Id), Guid.NewGuid()), CancellationToken.None))
            .WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

        // Assert
        sut.WaitForAssertion(() => queries.Should().Be(2));
    }

    [Fact]
    public async Task Does_not_render_again_when_only_the_page_renders()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        StateOf(_instanceId).Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running });
        ctx.Services.AddKeyedSingleton<TimeProvider>(Sdk.Constants.ClientTimeProviderServiceKey, new StoppedTimersTimeProvider());
        var cluster = Cluster((_instanceId, _engine));
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, cluster));
        sut.WaitForAssertion(() => sut.Find(".engine .state").TextContent.Should().Be(ClusterOverview.EngineRunning));
        var renders = sut.RenderCount;

        // Act
        sut.Render(p => p.Add(c => c.Cluster, cluster));

        // Assert
        sut.RenderCount.Should().Be(renders);
    }

    [Fact]
    public async Task Marks_an_engine_host_whose_chain_is_over_its_cycle_time()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        StateOf(_instanceId).Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running });
        var cluster = Cluster((_instanceId, _engine));
        var hostId = cluster.NodeGroups[0].Nodes[0].Applications[0].EngineHosts[0].Id;
        ctx.Services.GetRequiredService<EngineCycleHub>().Series.Add(hostId, new EngineCycleReport(650, []), DateTimeOffset.UtcNow);

        // Act
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, cluster));

        // Assert
        sut.WaitForAssertion(() =>
        {
            sut.Find(".host-head .chip.status-warning").TextContent.Should().Be(ClusterOverview.CycleTimeExceeded);
            sut.Find(".head-info .chip.status-warning").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.HostsOverCycleTime, 1));
            sut.Find(".host-cycle .cycle-value").ClassList.Should().Contain("over");
            sut.Find(".host-head .cycle-time").ClassList.Should().Contain("over");
        });
    }

    [Fact]
    public async Task A_chain_within_its_cycle_time_is_not_marked()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        StateOf(_instanceId).Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running });
        var cluster = Cluster((_instanceId, _engine));
        ctx.Services.GetRequiredService<EngineCycleHub>().Series.Add(cluster.NodeGroups[0].Nodes[0].Applications[0].EngineHosts[0].Id, new EngineCycleReport(120, []), DateTimeOffset.UtcNow);

        // Act
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, cluster));

        // Assert
        sut.FindAll(".chip.status-warning").Should().BeEmpty();
        sut.Find(".host-cycle .cycle-value").ClassList.Should().NotContain("over");
        sut.Find(".host-head .cycle-time").TextContent.Should().Be(string.Format(CultureInfo.CurrentCulture, ClusterOverview.HostCycleTime, 500));
        sut.Find(".host-head .cycle-time").ClassList.Should().NotContain("over");
        sut.Find(".host-cycle").GetAttribute("title").Should().Contain(string.Format(CultureInfo.CurrentCulture, ClusterOverview.HostCycleUsage,
            string.Format(CultureInfo.CurrentCulture, "{0:0.0} ms", 120.0), 500, 24));
    }

    [Theory]
    [InlineData(false, "enginehost")]
    [InlineData(true, "enginehost-privileged")]
    public async Task Links_the_journal_of_an_engine_host_on_this_instance_by_its_id_and_systemd_unit(bool elevatedPrivileges, string unit)
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        var cluster = Cluster((_instanceId, _engine));
        var host = cluster.NodeGroups[0].Nodes[0].Applications[0].EngineHosts[0];
        host.ElevatedPrivileges = elevatedPrivileges;

        // Act
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, cluster));

        // Assert
        sut.Find(".host-head a.log-link").GetAttribute("href").Should().Be(
            $"journal?filter=ENGINE_HOST_ID%3D{host.Id}%2B_SYSTEMD_UNIT%3D{unit}%40{host.Id}.service&follow=true");
        sut.FindAll(".engine .log-link").Should().BeEmpty();
    }

    [Fact]
    public async Task Does_not_link_the_journal_of_an_engine_host_on_another_instance()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);

        // Act
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, Cluster((_otherInstanceId, _otherEngine))));

        // Assert
        var logLink = sut.Find(".host-head .log-link");
        logLink.TagName.Should().Be("SPAN");
        logLink.GetAttribute("title").Should().Be(ClusterOverview.LogsOnOtherInstance);
    }

    [Fact]
    public async Task Stops_watching_cycle_reports_when_the_panel_is_disposed()
    {
        // Arrange
        await using var ctx = new BunitContext();
        Setup(ctx);
        var sut = ctx.Render<RuntimePanel>(p => p.Add(c => c.Cluster, Cluster((_instanceId, _engine))));
        _client.IsConnected.Returns(true);

        // Act
        await sut.Instance.DisposeAsync();

        // Assert
        await _client.Received(1).DisconnectAsync(Arg.Any<MQTTnet.MqttClientDisconnectOptions>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Never fires a timer; counts the timers created per due time.
    /// </summary>
    private sealed class ManualTimersTimeProvider : TimeProvider
    {
        private readonly ConcurrentBag<TimeSpan> _dueTimes = [];

        public int Count(TimeSpan dueTime) => _dueTimes.Count(d => d == dueTime);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _dueTimes.Add(dueTime);
            return System.CreateTimer(callback, state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Never fires a timer, so the panel renders only because of its parameters and the loaded states.
    /// </summary>
    private sealed class StoppedTimersTimeProvider : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => System.CreateTimer(callback, state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private Task<GetEngineStateOnInstanceResponse> StateOf(Guid instanceId)
        => _mediator.Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(
            Arg.Any<GetEngineStateOnInstanceRequest>(), instanceId, Arg.Any<CancellationToken>());

    private static ILogger<RuntimePanel> AddLogger(BunitContext ctx)
    {
        var logger = Substitute.For<ILogger<RuntimePanel>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        ctx.Services.AddSingleton(logger);
        return logger;
    }

    private static List<LogLevel> LogLevels(ILogger logger)
        => [.. logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(ILogger.Log))
            .Select(c => (LogLevel)c.GetArguments()[0]!)];

    private void Setup(BunitContext ctx)
    {
        ctx.SetupSuiteServices(setup => _mediator = setup.ClientMediator);

        var brokerProvider = Substitute.For<IEngineHostBrokerProvider>();
        brokerProvider.FindBrokers(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, EngineHostBroker>
            {
                [_instanceId] = new(Guid.NewGuid(), new MqttConnection { Address = "localhost" },
                    new MQTTnet.MqttClientOptionsBuilder().WithTcpServer("localhost").Build()),
            });

        var instanceInformation = Substitute.For<IInstanceInformationProvider>();
        instanceInformation.Local.Id.Returns(_instanceId);

        ctx.Services.AddClientTimeProvider();
        ctx.Services.AddSingleton(instanceInformation);
        ctx.Services.AddSingleton(_ => new EngineCycleHub(() => _client, TimeProvider.System, Substitute.For<ILogger<EngineCycleHub>>()));
        ctx.Services.AddSingleton(services => new EngineCycleMonitor(brokerProvider, services.GetRequiredService<EngineCycleHub>(),
            Substitute.For<ILogger<EngineCycleMonitor>>()));
    }

    private static Cluster Cluster(params (Guid InstanceId, Engine Engine)[] engines)
        => new()
        {
            NodeGroups =
            [
                new ClusterNodeGroup
                {
                    Nodes =
                    [
                        .. engines.Select(e => new ClusterNode
                        {
                            Applications =
                            [
                                new ClusterApplication
                                {
                                    Id = e.InstanceId,
                                    Type = ClusterApplicationType.CoreOsStandalone,
                                    EngineHosts = [new EngineHost { Id = Guid.NewGuid(), Name = "DCW-Host", Engines = [e.Engine] }],
                                },
                            ],
                        }),
                    ],
                },
            ],
        };
}
