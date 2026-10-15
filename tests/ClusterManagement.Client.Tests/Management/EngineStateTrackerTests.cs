using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.ErrorHandling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;

namespace ClusterManagement.Client.Tests.Management;

public class EngineStateTracker_DisposeAsync
{
    private readonly Guid _instanceId = Guid.NewGuid();
    private readonly Guid _engineId = Guid.NewGuid();
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Fact]
    public async Task An_answer_after_the_owner_closed_changes_nothing_and_logs_nothing_Async()
    {
        var answer = new TaskCompletionSource<GetEngineStateOnInstanceResponse>();
        _mediator.Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(Arg.Any<GetEngineStateOnInstanceRequest>(), _instanceId, Arg.Any<CancellationToken>())
            .Returns(answer.Task);
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var changes = 0;
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), TimeProvider.System, Substitute.For<ILogger<EngineCycleHub>>());
        var tracker = new EngineStateTracker(_mediator, Monitor(hub), TimeProvider.System, _logger, work => work(), () => changes++);
        var tracking = tracker.Track([new EngineHostRuntimeModel(Guid.NewGuid(), _instanceId, "Line-Host", false,
            [new EngineRuntimeModel(_instanceId, _engineId, "Filling", 100)])]);
        var changesBeforeClosing = changes;

        await tracker.DisposeAsync();
        answer.SetException(new TimeoutException());
        await tracking;

        changes.Should().Be(changesBeforeClosing);
        tracker.Hosts[0].Engines[0].IsChecked.Should().BeFalse();
        _logger.ReceivedCalls().Where(call => call.GetMethodInfo().Name == nameof(ILogger.Log)).Should().BeEmpty();
    }

    [Fact]
    public async Task An_engine_event_after_the_owner_closed_asks_nothing_Async()
    {
        _mediator.Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(Arg.Any<GetEngineStateOnInstanceRequest>(), _instanceId, Arg.Any<CancellationToken>())
            .Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running });
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), TimeProvider.System, Substitute.For<ILogger<EngineCycleHub>>());
        Task? reload = null;
        var tracker = new EngineStateTracker(_mediator, Monitor(hub), TimeProvider.System, _logger, work => reload = work(), () => { });
        await tracker.Track([Host()]);
        await tracker.DisposeAsync();
        _mediator.ClearReceivedCalls();

        tracker.Reload(_engineId);

        await reload!;
        _mediator.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task The_end_of_a_deployment_after_the_owner_closed_asks_nothing_and_changes_nothing_Async()
    {
        var changes = 0;
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), TimeProvider.System, Substitute.For<ILogger<EngineCycleHub>>());
        var tracker = new EngineStateTracker(_mediator, Monitor(hub), TimeProvider.System, _logger, work => work(), () => changes++);
        await tracker.Track([Host()], Guid.NewGuid());
        await tracker.DisposeAsync();
        var changesBeforeClosing = changes;

        await tracker.SetDeployment(null);

        changes.Should().Be(changesBeforeClosing);
        _mediator.ReceivedCalls().Should().BeEmpty();
    }

    private EngineHostRuntimeModel Host()
        => new(Guid.NewGuid(), _instanceId, "Line-Host", false, [new EngineRuntimeModel(_instanceId, _engineId, "Filling", 100)]);

    internal static EngineCycleMonitor Monitor(EngineCycleHub hub)
    {
        var brokerProvider = Substitute.For<IEngineHostBrokerProvider>();
        brokerProvider.FindBrokers(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, EngineHostBroker>());
        return new EngineCycleMonitor(brokerProvider, hub, Substitute.For<ILogger<EngineCycleMonitor>>());
    }
}

public class EngineStateTracker_Track
{
    private readonly Guid _instanceId = Guid.NewGuid();
    private readonly Guid _engineId = Guid.NewGuid();
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task Asks_an_engine_whose_host_does_not_answer_again_ten_seconds_after_each_failed_answer_Async()
    {
        StateRequest().Returns<GetEngineStateOnInstanceResponse>(_ => throw new TimeoutException());
        await using var hub = Hub();
        await using var tracker = Tracker(hub);
        await tracker.Track(Hosts());

        _time.Advance(TimeSpan.FromSeconds(10));
        await Eventually(() => StateRequests() == 2);
        _time.Advance(TimeSpan.FromSeconds(10));
        await Eventually(() => StateRequests() == 3);

        tracker.Hosts[0].Engines[0].IsReachable.Should().BeFalse();
    }

    [Fact]
    public async Task Ignores_a_late_answer_for_the_hosts_tracked_before_Async()
    {
        var late = new TaskCompletionSource<GetEngineStateOnInstanceResponse>();
        StateRequest().Returns(late.Task, Task.FromResult(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running }));
        await using var hub = Hub();
        await using var tracker = Tracker(hub);
        var before = tracker.Track(Hosts());
        await tracker.Track(Hosts());

        late.SetResult(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Stopped });
        await before;

        tracker.Hosts[0].Engines[0].State.Should().Be(EngineState.Running);
    }

    [Fact]
    public async Task Stops_the_instance_asking_an_engine_host_that_does_not_answer_in_time_Async()
    {
        var request = CancellationToken.None;
        StateRequest().Returns(call =>
        {
            request = call.Arg<CancellationToken>();
            return new TaskCompletionSource<GetEngineStateOnInstanceResponse>().Task;
        });
        await using var hub = Hub();
        await using var tracker = Tracker(hub);
        var tracking = tracker.Track(Hosts());

        _time.Advance(TimeSpan.FromSeconds(10));
        await tracking;

        request.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Warns_when_the_instance_could_not_get_the_state_of_its_engine_Async()
    {
        StateRequest().Returns(new GetEngineStateOnInstanceResponse(new ErrorInfo(ClusterErrors.EngineHostDidNotAnswer, "No answer")));
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        await using var hub = Hub();
        await using var tracker = Tracker(hub);

        await tracker.Track(Hosts());

        tracker.Hosts[0].Engines[0].IsReachable.Should().BeFalse();
        _logger.ReceivedCalls().Where(call => call.GetMethodInfo().Name == nameof(ILogger.Log))
            .Should().ContainSingle(call => (LogLevel)call.GetArguments()[0]! == LogLevel.Warning);
    }

    private Task<GetEngineStateOnInstanceResponse> StateRequest()
        => _mediator.Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(Arg.Any<GetEngineStateOnInstanceRequest>(), _instanceId, Arg.Any<CancellationToken>());

    private int StateRequests()
        => _mediator.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IUiMediator.Request));

    // Timers of the fake clock complete their tasks, whose continuations may run on another thread.
    private static async Task Eventually(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
            await Task.Delay(10, TestContext.Current.CancellationToken);

        condition().Should().BeTrue();
    }

    private EngineCycleHub Hub()
        => new(() => Substitute.For<MQTTnet.IMqttClient>(), _time, Substitute.For<ILogger<EngineCycleHub>>());

    private EngineStateTracker Tracker(EngineCycleHub hub)
        => new(_mediator, EngineStateTracker_DisposeAsync.Monitor(hub), _time, _logger, work => work(), () => { });

    private List<EngineHostRuntimeModel> Hosts()
        => [new EngineHostRuntimeModel(Guid.NewGuid(), _instanceId, "Line-Host", false, [new EngineRuntimeModel(_instanceId, _engineId, "Filling", 100)])];
}

public class EngineStateTracker_Reload
{
    private readonly Guid _instanceId = Guid.NewGuid();
    private readonly Guid _engineId = Guid.NewGuid();
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task Keeps_the_newer_state_when_an_older_answer_arrives_later_Async()
    {
        var firstAnswer = new TaskCompletionSource<GetEngineStateOnInstanceResponse>();
        var queries = 0;
        _mediator.Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(Arg.Any<GetEngineStateOnInstanceRequest>(), _instanceId, Arg.Any<CancellationToken>())
            .Returns(_ => ++queries == 1 ? firstAnswer.Task : Task.FromResult(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running }));
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), _time, Substitute.For<ILogger<EngineCycleHub>>());
        await using var tracker = new EngineStateTracker(_mediator, EngineStateTracker_DisposeAsync.Monitor(hub), _time, Substitute.For<ILogger>(), work => work(), () => { });
        var tracking = tracker.Track([new EngineHostRuntimeModel(Guid.NewGuid(), _instanceId, "Line-Host", false,
            [new EngineRuntimeModel(_instanceId, _engineId, "Filling", 100)])]);
        tracker.Reload(_engineId);

        firstAnswer.SetException(new TimeoutException());
        await tracking;

        tracker.Hosts[0].Engines[0].State.Should().Be(EngineState.Running);
    }
}

public class EngineStateTracker_SetDeployment
{
    private readonly Guid _instanceId = Guid.NewGuid();
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();

    [Fact]
    public async Task Asks_no_engine_while_a_deployment_runs_Async()
    {
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), TimeProvider.System, Substitute.For<ILogger<EngineCycleHub>>());
        await using var tracker = Tracker(hub);

        await tracker.Track(Hosts(), Guid.NewGuid());

        await _mediator.DidNotReceiveWithAnyArgs().Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(default!, default, TestContext.Current.CancellationToken);
        tracker.CheckedAt.Should().BeNull();
    }

    [Fact]
    public async Task Keeps_a_deployment_that_started_while_the_hosts_were_loaded_Async()
    {
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), TimeProvider.System, Substitute.For<ILogger<EngineCycleHub>>());
        await using var tracker = Tracker(hub);
        await tracker.SetDeployment(Guid.NewGuid());

        await tracker.Track(Hosts());

        tracker.IsDeploying.Should().BeTrue();
        await _mediator.DidNotReceiveWithAnyArgs().Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Asks_every_engine_once_the_deployment_ended_Async()
    {
        _mediator.Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(Arg.Any<GetEngineStateOnInstanceRequest>(), _instanceId, Arg.Any<CancellationToken>())
            .Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running });
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), TimeProvider.System, Substitute.For<ILogger<EngineCycleHub>>());
        await using var tracker = Tracker(hub);
        await tracker.Track(Hosts(), Guid.NewGuid());

        await tracker.SetDeployment(null);

        tracker.Hosts[0].Engines.Should().AllSatisfy(engine => engine.State.Should().Be(EngineState.Running));
        await _mediator.Received(2).Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(Arg.Any<GetEngineStateOnInstanceRequest>(), _instanceId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Asks_every_engine_once_the_deployment_that_holds_them_ended_Async()
    {
        _mediator.Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(Arg.Any<GetEngineStateOnInstanceRequest>(), _instanceId, Arg.Any<CancellationToken>())
            .Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running });
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), TimeProvider.System, Substitute.For<ILogger<EngineCycleHub>>());
        await using var tracker = Tracker(hub);
        var deployment = Guid.NewGuid();
        await tracker.Track(Hosts(), deployment);

        await tracker.EndDeployment(deployment);

        tracker.IsDeploying.Should().BeFalse();
        await _mediator.Received(2).Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(Arg.Any<GetEngineStateOnInstanceRequest>(), _instanceId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Keeps_the_engines_held_when_another_deployment_ends_Async()
    {
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), TimeProvider.System, Substitute.For<ILogger<EngineCycleHub>>());
        await using var tracker = Tracker(hub);
        await tracker.Track(Hosts(), Guid.NewGuid());

        await tracker.EndDeployment(Guid.NewGuid());

        tracker.IsDeploying.Should().BeTrue();
        await _mediator.DidNotReceiveWithAnyArgs().Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(default!, default, TestContext.Current.CancellationToken);
    }

    private EngineStateTracker Tracker(EngineCycleHub hub)
        => new(_mediator, EngineStateTracker_DisposeAsync.Monitor(hub), TimeProvider.System, Substitute.For<ILogger>(), work => work(), () => { });

    private List<EngineHostRuntimeModel> Hosts()
        => [new EngineHostRuntimeModel(Guid.NewGuid(), _instanceId, "Line-Host", false,
            [new EngineRuntimeModel(_instanceId, Guid.NewGuid(), "Filling", 100), new EngineRuntimeModel(_instanceId, Guid.NewGuid(), "Capping", 100)])];
}

public class EngineStateTracker_CheckReporting
{
    private readonly Guid _instanceId = Guid.NewGuid();
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly FakeTimeProvider _time = new();

    public EngineStateTracker_CheckReporting()
        => _mediator.Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(Arg.Any<GetEngineStateOnInstanceRequest>(), _instanceId, Arg.Any<CancellationToken>())
            .Returns(new GetEngineStateOnInstanceResponse { EngineState = EngineState.Running });

    [Fact]
    public async Task Holds_the_engines_while_a_deployment_runs_shorter_than_two_minutes_Async()
    {
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), _time, Substitute.For<ILogger<EngineCycleHub>>());
        await using var tracker = Tracker(hub);
        await tracker.Track(Hosts(), Guid.NewGuid());

        tracker.CheckReporting(_time.GetUtcNow() + TimeSpan.FromSeconds(119));

        tracker.IsDeploying.Should().BeTrue();
        await _mediator.DidNotReceiveWithAnyArgs().Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Asks_every_engine_once_a_deployment_held_them_for_two_minutes_Async()
    {
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), _time, Substitute.For<ILogger<EngineCycleHub>>());
        await using var tracker = Tracker(hub);
        await tracker.Track(Hosts(), Guid.NewGuid());

        tracker.CheckReporting(_time.GetUtcNow() + TimeSpan.FromMinutes(2));

        tracker.IsDeploying.Should().BeFalse();
        await _mediator.Received(2).Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(Arg.Any<GetEngineStateOnInstanceRequest>(), _instanceId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_not_hold_the_engines_again_while_the_same_deployment_runs_Async()
    {
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), _time, Substitute.For<ILogger<EngineCycleHub>>());
        await using var tracker = Tracker(hub);
        var deployment = Guid.NewGuid();
        await tracker.Track(Hosts(), deployment);
        tracker.CheckReporting(_time.GetUtcNow() + TimeSpan.FromMinutes(2));

        await tracker.SetDeployment(deployment);

        tracker.IsDeploying.Should().BeFalse();
    }

    [Fact]
    public async Task Holds_the_engines_again_when_another_deployment_starts_after_a_hung_one_Async()
    {
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), _time, Substitute.For<ILogger<EngineCycleHub>>());
        await using var tracker = Tracker(hub);
        await tracker.Track(Hosts(), Guid.NewGuid());
        tracker.CheckReporting(_time.GetUtcNow() + TimeSpan.FromMinutes(2));

        await tracker.SetDeployment(Guid.NewGuid());

        tracker.IsDeploying.Should().BeTrue();
    }

    private EngineStateTracker Tracker(EngineCycleHub hub)
        => new(_mediator, EngineStateTracker_DisposeAsync.Monitor(hub), _time, Substitute.For<ILogger>(), work => work(), () => { });

    private List<EngineHostRuntimeModel> Hosts()
        => [new EngineHostRuntimeModel(Guid.NewGuid(), _instanceId, "Line-Host", false,
            [new EngineRuntimeModel(_instanceId, Guid.NewGuid(), "Filling", 100), new EngineRuntimeModel(_instanceId, Guid.NewGuid(), "Capping", 100)])];
}

public class EngineStateTracker_DeploymentAlive
{
    private readonly Guid _instanceId = Guid.NewGuid();
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task Keeps_the_engines_held_while_the_deployment_reports_progress_Async()
    {
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), _time, Substitute.For<ILogger<EngineCycleHub>>());
        await using var tracker = Tracker(hub);
        var deployment = Guid.NewGuid();
        var started = _time.GetUtcNow();
        await tracker.Track(Hosts(), deployment);
        _time.Advance(TimeSpan.FromSeconds(90));

        tracker.DeploymentAlive(deployment);
        tracker.CheckReporting(started + TimeSpan.FromMinutes(3));

        tracker.IsDeploying.Should().BeTrue();
        await _mediator.DidNotReceiveWithAnyArgs().Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Asks_every_engine_two_minutes_after_the_deployment_last_reported_progress_Async()
    {
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), _time, Substitute.For<ILogger<EngineCycleHub>>());
        await using var tracker = Tracker(hub);
        var deployment = Guid.NewGuid();
        await tracker.Track(Hosts(), deployment);
        _time.Advance(TimeSpan.FromSeconds(90));

        tracker.DeploymentAlive(deployment);
        tracker.CheckReporting(_time.GetUtcNow() + TimeSpan.FromMinutes(2));

        tracker.IsDeploying.Should().BeFalse();
    }

    [Fact]
    public async Task Ignores_the_progress_of_another_deployment_Async()
    {
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), _time, Substitute.For<ILogger<EngineCycleHub>>());
        await using var tracker = Tracker(hub);
        var started = _time.GetUtcNow();
        await tracker.Track(Hosts(), Guid.NewGuid());
        _time.Advance(TimeSpan.FromSeconds(90));

        tracker.DeploymentAlive(Guid.NewGuid());
        tracker.CheckReporting(started + TimeSpan.FromMinutes(2));

        tracker.IsDeploying.Should().BeFalse();
    }

    [Fact]
    public async Task Does_not_hold_the_engines_again_once_the_hold_is_over_Async()
    {
        await using var hub = new EngineCycleHub(() => Substitute.For<MQTTnet.IMqttClient>(), _time, Substitute.For<ILogger<EngineCycleHub>>());
        await using var tracker = Tracker(hub);
        var deployment = Guid.NewGuid();
        await tracker.Track(Hosts(), deployment);
        tracker.CheckReporting(_time.GetUtcNow() + TimeSpan.FromMinutes(2));

        tracker.DeploymentAlive(deployment);

        tracker.IsDeploying.Should().BeFalse();
    }

    private EngineStateTracker Tracker(EngineCycleHub hub)
        => new(_mediator, EngineStateTracker_DisposeAsync.Monitor(hub), _time, Substitute.For<ILogger>(), work => work(), () => { });

    private List<EngineHostRuntimeModel> Hosts()
        => [new EngineHostRuntimeModel(Guid.NewGuid(), _instanceId, "Line-Host", false,
            [new EngineRuntimeModel(_instanceId, Guid.NewGuid(), "Filling", 100)])];
}
