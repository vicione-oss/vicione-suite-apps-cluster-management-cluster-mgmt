using System.Globalization;
using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Internal;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.ErrorHandling;
using ClusterManagement.Public.Events;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Authorization;
using Sdk.Client.Infrastructure;
using Sdk.Instance;
using Sdk.Utils;
using ViciOne.Cluster.Model;
using ViciOne.Ui.Blazor.Components.Button.Enums;
using ViciOne.Ui.Blazor.Components.Button.Extensions;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace ClusterManagement.Client.Management.Components;

public sealed partial class RuntimePanel : IAsyncDisposable,
    IEventConsumer<EngineStarted>,
    IEventConsumer<EngineStopped>,
    IEventConsumer<EngineTornDown>
{
    // Route of the suite's journal page, which takes a journalctl-style filter.
    private const string JournalRoute = "journal";

    // The suite's journal page requires this feature of the system module; the Sdk does not publish its name.
    private const string LogViewerFeature = "Log viewer";

    private readonly string _refreshIconCssClass = MonochromeIconName.Refresh.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private static readonly TimeSpan s_chartInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan s_renderDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan s_stateTimeout = TimeSpan.FromSeconds(10);

    private readonly CancellationTokenSource _tokenSource = new();
    private readonly AutoDisposeList<IDisposable> _subscriptions = [];

    // Several queries for one engine can overlap (a retry, an engine event, a refresh); only the newest one counts.
    private readonly Dictionary<Guid, int> _latestQueries = [];

    // An engine whose host does not answer is asked again once, however many events arrive for it meanwhile.
    private readonly HashSet<(Guid EngineId, int Generation)> _pendingRetries = [];

    private Cluster? _loadedCluster;
    private IReadOnlyList<EngineHostRuntimeModel> _hosts = [];
    private DateTimeOffset? _checkedAt;
    private DateTimeOffset _now;
    private bool _isLoading;
    private int _reloadGeneration;
    private bool _hasChanges = true;
    private bool _isRenderScheduled;

    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] private EngineCycleMonitor CycleMonitor { get; set; } = default!;
    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)] private TimeProvider TimeProvider { get; set; } = default!;
    [Inject] private IInstanceInformationProvider InstanceInformation { get; set; } = default!;
    [Inject] private ILogger<RuntimePanel> Logger { get; set; } = default!;

    [Parameter, EditorRequired] public Cluster Cluster { get; set; } = default!;

    internal static string? LogViewerPolicy { get; }
        = ModulePolicyProvider.GetPolicy(Sdk.Constants.SystemModuleId, AccessLevel.Full, LogViewerFeature);

    private string CheckedText => _checkedAt is { } checkedAt
        ? string.Format(CultureInfo.CurrentCulture, ClusterOverview.CheckedAt, checkedAt.To24HourTime(TimeProvider.LocalTimeZone))
        : "…";

    private string EnginesStatusName => _checkedAt is null ? "previous" : RunningCount == EngineCount ? "running" : "warning";

    /// <summary>
    /// Engine hosts whose chain took longer than its cycle time in the shown window; the engines of a host share that
    /// cycle, so the chain can be over it while each engine alone is not.
    /// </summary>
    private int HostsOverCycleTime => _hosts.Count(host => IsOverCycleTime(ChainWindow(host), host.CycleTime));

    private int EngineCount => _hosts.Sum(h => h.Engines.Count);
    private int RunningCount => _hosts.Sum(h => h.Engines.Count(e => e.IsRunning));

    protected override void OnInitialized()
    {
        _now = TimeProvider.GetUtcNow();
        _subscriptions.Add(Mediator.Register<EngineStarted>(this));
        _subscriptions.Add(Mediator.Register<EngineStopped>(this));
        _subscriptions.Add(Mediator.Register<EngineTornDown>(this));
    }

    protected override async Task OnParametersSetAsync()
    {
        if (ReferenceEquals(_loadedCluster, Cluster))
            return;

        _loadedCluster = Cluster;
        _hosts = EngineHostRuntimeModel.FromCluster(Cluster);
        _hasChanges = true;

        // A broker that is slow to connect must not hold back the engine states.
        await Task.WhenAll(CycleMonitor.Watch(_hosts, _tokenSource.Token), ReloadStates());
    }

    // The page renders for every deployment event; the panel only follows its own changes, which keeps its charts
    // out of those renders.
    protected override bool ShouldRender()
        => _hasChanges;

    protected override void OnAfterRender(bool firstRender)
    {
        _hasChanges = false;

        if (firstRender)
            _ = RefreshChartsPeriodically(_tokenSource.Token);
    }

    public async ValueTask DisposeAsync()
    {
        _subscriptions.Dispose();

        try
        {
            await _tokenSource.CancelAsync();
            _tokenSource.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Ignore: CancellationTokenSource was already disposed.
        }

        await CycleMonitor.Stop();
    }

    public Task Consume(ClientContext<EngineStarted> context, CancellationToken cancellationToken)
        => StartReload(context.Message.EngineId);

    public Task Consume(ClientContext<EngineStopped> context, CancellationToken cancellationToken)
        => StartReload(context.Message.EngineId);

    public Task Consume(ClientContext<EngineTornDown> context, CancellationToken cancellationToken)
        => StartReload(context.Message.EngineId);

    // The UI events of an instance arrive through one receive endpoint, one at a time on Standalone; waiting for an engine
    // host that does not answer would hold back every other UI event until the state request times out.
    private Task StartReload(Guid engineId)
    {
        _ = InvokeAsync(() => ReloadEngine(engineId));
        return Task.CompletedTask;
    }

    private async Task RefreshChartsPeriodically(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(s_chartInterval, TimeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await InvokeAsync(() =>
                {
                    _now = TimeProvider.GetUtcNow();
                    _hasChanges = true;
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException)
        {
            // The panel was disposed.
        }
    }

    private async Task ReloadStates()
    {
        // A slower reload for a previous cluster must not overwrite the hosts of the current one.
        var generation = ++_reloadGeneration;
        _isLoading = true;
        _hasChanges = true;

        await Task.WhenAll(_hosts.SelectMany(h => h.Engines).Select(engine => ReloadState(engine, generation)));
        if (generation != _reloadGeneration)
            return;

        _checkedAt = TimeProvider.GetUtcNow();
        _isLoading = false;
        RequestRender();
    }

    // An engine event concerns a single engine; the others keep their state.
    private async Task ReloadEngine(Guid engineId)
    {
        var engine = _hosts.SelectMany(h => h.Engines).FirstOrDefault(e => e.EngineId == engineId);
        if (engine is not null)
            await ReloadState(engine, _reloadGeneration);
    }

    // Each state is shown as soon as it arrives: an instance that does not answer would otherwise hold back
    // every other engine until its request times out.
    private async Task ReloadState(EngineRuntimeModel engine, int generation)
    {
        var query = _latestQueries[engine.EngineId] = _latestQueries.GetValueOrDefault(engine.EngineId) + 1;
        var checkedEngine = await QueryState(engine);
        if (generation != _reloadGeneration || query != _latestQueries[engine.EngineId])
            return;

        _hosts = EngineHostRuntimeModel.WithEngine(_hosts, checkedEngine);
        RequestRender();

        if (checkedEngine is { IsReachable: false, IsDeployed: true } && _pendingRetries.Add((checkedEngine.EngineId, generation)))
            _ = AskAgainLater(checkedEngine.EngineId, generation, _tokenSource.Token);
    }

    // An engine host that was restarting when it was asked answers again later, often without an engine event.
    private async Task AskAgainLater(Guid engineId, int generation, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(s_stateTimeout, TimeProvider, cancellationToken);
            await InvokeAsync(async () =>
            {
                _pendingRetries.Remove((engineId, generation));
                if (generation == _reloadGeneration)
                    await ReloadEngine(engineId);
            });
        }
        catch (OperationCanceledException)
        {
            // The panel was disposed.
        }
    }

    // Answers of many engines arrive close together; they are rendered together instead of one render each.
    private void RequestRender()
    {
        _hasChanges = true;
        if (_isRenderScheduled)
            return;

        _isRenderScheduled = true;
        _ = RenderAfterDelay(_tokenSource.Token);
    }

    private async Task RenderAfterDelay(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(s_renderDelay, TimeProvider, cancellationToken);
            await InvokeAsync(() =>
            {
                _isRenderScheduled = false;
                StateHasChanged();
            });
        }
        catch (OperationCanceledException)
        {
            // The panel was disposed.
        }
    }

    // An engine host that is restarted while it is asked may never answer; the panel must not wait for it forever.
    private async Task<EngineRuntimeModel> QueryState(EngineRuntimeModel engine)
    {
        try
        {
            var response = await Mediator.Request<GetEngineStateOnInstanceRequest, GetEngineStateOnInstanceResponse>(
                    new(engine.EngineId), engine.InstanceId, _tokenSource.Token)
                .WaitAsync(s_stateTimeout, TimeProvider, _tokenSource.Token);

            // An instance without a deployment of the engine answered as well.
            var isDeployed = response.RequestError?.ErrorCode != ClusterErrors.EngineNotDeployed;
            return engine with
            {
                IsChecked = true,
                State = response.EngineState,
                IsReachable = response.RequestError is null || !isDeployed,
                IsDeployed = isDeployed,
                DeploymentId = response.DeploymentId ?? engine.DeploymentId,
            };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // An engine host that stays unreachable is asked again and again; only the change is worth a warning.
            if (engine is { IsChecked: true, IsReachable: false })
                LogEngineStateErrorAgain(Logger, e, engine.EngineId);
            else
                LogEngineStateError(Logger, e, engine.EngineId);

            return engine with { IsChecked = true, State = null, IsReachable = false };
        }
    }

    private IReadOnlyList<double?> ChainWindow(EngineHostRuntimeModel host)
        => CycleMonitor.Series.Window(host.Id, _now);

    // Explains the host's cycle and, once one was reported, how much of the cycle time it used.
    private static string HostCycleTitle(EngineHostRuntimeModel host, double? latestChainCycle)
        => latestChainCycle is { } latest && host.CycleTime > 0
            ? $"{ClusterOverview.HostCycleHint}: {string.Format(CultureInfo.CurrentCulture, ClusterOverview.HostCycleUsage, FormatMilliseconds(latest), host.CycleTime, latest / host.CycleTime * 100)}"
            : ClusterOverview.HostCycleHint;

    private static bool IsOverCycleTime(IReadOnlyList<double?> window, uint cycleTime)
        => cycleTime > 0 && window.Any(sample => sample > cycleTime);

    private double? LatestCycle(EngineRuntimeModel engine)
        => engine.DeploymentId is { } deploymentId ? CycleMonitor.Series.Latest(deploymentId, _now) : null;

    // The chart keeps its place before the deployment id is known, so the panel does not grow when it arrives.
    private IReadOnlyList<double?> CycleWindow(EngineRuntimeModel engine)
        => engine.DeploymentId is { } deploymentId
            ? CycleMonitor.Series.Window(deploymentId, _now)
            : new double?[CycleMonitor.Series.Seconds];

    // The journal page shows the journal of the instance serving this page only.
    private bool HasLocalJournal(EngineHostRuntimeModel host)
        => host.InstanceId == InstanceInformation.Local.Id;

    // The host writes its id as a journal field; its engines do not write theirs as fields, so their entries are found
    // by the systemd unit the host runs in, which journald adds to every entry of the process. '+' is OR.
    private static string JournalLink(EngineHostRuntimeModel host)
    {
        var filter = $"ENGINE_HOST_ID={host.Id}+_SYSTEMD_UNIT={EngineHostUnits.Of(host.Id, host.ElevatedPrivileges)}";
        return $"{JournalRoute}?filter={Uri.EscapeDataString(filter)}&follow=true";
    }

    private static string FormatMilliseconds(double? milliseconds) => milliseconds switch
    {
        null => "–",
        < 1 => string.Format(CultureInfo.CurrentCulture, "{0:0.00} ms", milliseconds),
        _ => string.Format(CultureInfo.CurrentCulture, "{0:0.0} ms", milliseconds),
    };

    private static string EngineStatusName(EngineRuntimeModel engine) => engine switch
    {
        { IsDeployed: false } => "previous",
        { IsReachable: false } => "failed",
        { State: EngineState.Running } => "running",
        { State: EngineState.Starting or EngineState.Stopping } => "deploying",
        _ => "previous",
    };

    private static string EngineStateText(EngineRuntimeModel engine) => engine switch
    {
        { IsDeployed: false } => ClusterOverview.EngineNotDeployed,
        { IsReachable: false } => ClusterOverview.EngineNotReachable,
        { State: EngineState.Running } => ClusterOverview.EngineRunning,
        { State: EngineState.Starting } => ClusterOverview.EngineStarting,
        { State: EngineState.Stopping } => ClusterOverview.EngineStopping,
        _ => ClusterOverview.EngineStopped,
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get the state of engine {EngineId}")]
    private static partial void LogEngineStateError(ILogger logger, Exception exception, Guid engineId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed again to get the state of engine {EngineId}")]
    private static partial void LogEngineStateErrorAgain(ILogger logger, Exception exception, Guid engineId);
}
