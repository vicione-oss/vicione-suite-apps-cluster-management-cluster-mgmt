using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.Management.Extensions;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Internal;
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
    IEventConsumer<EngineTornDown>,
    IEventConsumer<ClusterUpdateChanged>,
    IEventConsumer<ClusterNodeDeploymentStepChanged>
{
    // Route of the suite's journal page, which takes a journalctl-style filter.
    private const string JournalRoute = "journal";

    // The suite's journal page requires this feature of the system module; the Sdk does not publish its name.
    private const string LogViewerFeature = "Log viewer";

    private readonly string _openIconCssClass = MonochromeIconName.ArrowNavigationLightRight.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private static readonly TimeSpan s_chartInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan s_renderDelay = TimeSpan.FromMilliseconds(100);

    private readonly CancellationTokenSource _tokenSource = new();
    private readonly AutoDisposeList<IDisposable> _subscriptions = [];

    private Cluster? _loadedCluster;
    private EngineStateTracker _tracker = default!;
    private DateTimeOffset _now;
    private bool _hasChanges = true;
    private bool _isRenderScheduled;
    private bool _renderedExpert;

    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] private EngineCycleMonitor CycleMonitor { get; set; } = default!;
    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)] private TimeProvider TimeProvider { get; set; } = default!;
    [Inject] private IInstanceInformationProvider InstanceInformation { get; set; } = default!;
    [Inject] private ILogger<RuntimePanel> Logger { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter, EditorRequired] public Cluster Cluster { get; set; } = default!;

    /// <summary>
    /// Adds the cycle times, their charts, the privileged chip, the journal links and the way to the engines.
    /// </summary>
    [Parameter] public bool Expert { get; set; }

    /// <summary>
    /// The correlation id of the deployment of the cluster while it runs; the engines are asked again once it ends.
    /// </summary>
    [Parameter] public Guid? Deployment { get; set; }

    internal static string? LogViewerPolicy { get; }
        = ModulePolicyProvider.GetPolicy(Sdk.Constants.SystemModuleId, AccessLevel.Full, LogViewerFeature);

    private IReadOnlyList<EngineHostRuntimeModel> Hosts => _tracker.Hosts;

    private string EnginesStatusName => _tracker.IsDeploying ? "deploying"
        : _tracker.CheckedAt is null ? "previous"
        : RunningCount == EngineCount ? "running" : "warning";

    /// <summary>
    /// Engine hosts whose chain took longer than its cycle time in the shown window; the engines of a host share that
    /// cycle, so the chain can be over it while each engine alone is not.
    /// </summary>
    private int HostsOverCycleTime => Hosts.Count(host => host.IsOverCycleTime(ChainWindow(host)));

    private List<EngineRuntimeModel> AllEngines => [.. Hosts.SelectMany(h => h.Engines)];

    private int EngineCount => Hosts.Sum(h => h.Engines.Count);
    private int RunningCount => Hosts.Sum(h => h.Engines.Count(e => e.IsRunning));

    protected override void OnInitialized()
    {
        _now = TimeProvider.GetUtcNow();
        _tracker = new EngineStateTracker(Mediator, CycleMonitor, TimeProvider, Logger, InvokeAsync, RequestRender);
        _subscriptions.Add(Mediator.Register<EngineStarted>(this));
        _subscriptions.Add(Mediator.Register<EngineStopped>(this));
        _subscriptions.Add(Mediator.Register<EngineTornDown>(this));
        _subscriptions.Add(Mediator.Register<ClusterUpdateChanged>(this));
        _subscriptions.Add(Mediator.Register<ClusterNodeDeploymentStepChanged>(this));
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_renderedExpert != Expert)
        {
            _renderedExpert = Expert;
            _hasChanges = true;
        }

        if (ReferenceEquals(_loadedCluster, Cluster))
        {
            await _tracker.SetDeployment(Deployment);
            return;
        }

        _loadedCluster = Cluster;
        _hasChanges = true;
        await _tracker.Track(EngineHostRuntimeModel.FromCluster(Cluster), Deployment);

        // The version a deployment brought arrives with its end; its engines are asked once the deployment is over.
        await _tracker.SetDeployment(Deployment);
    }

    // The page renders for every deployment event; the panel only follows its own changes, which keeps its charts
    // out of those renders.
    protected override bool ShouldRender()
        => _hasChanges;

    private void OpenEngines()
        => Navigation.NavigateToEngines();

    protected override void OnAfterRender(bool firstRender)
    {
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

        await _tracker.DisposeAsync();
    }

    public Task Consume(ClientContext<EngineStarted> context, CancellationToken cancellationToken)
        => Reload(context.Message.EngineId);

    public Task Consume(ClientContext<EngineStopped> context, CancellationToken cancellationToken)
        => Reload(context.Message.EngineId);

    public Task Consume(ClientContext<EngineTornDown> context, CancellationToken cancellationToken)
        => Reload(context.Message.EngineId);

    public Task Consume(ClientContext<ClusterUpdateChanged> context, CancellationToken cancellationToken)
        => DeploymentAlive(context.Message.CorrelationId);

    public Task Consume(ClientContext<ClusterNodeDeploymentStepChanged> context, CancellationToken cancellationToken)
        => DeploymentAlive(context.Message.CorrelationId);

    private Task DeploymentAlive(Guid deployment)
        => InvokeAsync(() => _tracker.DeploymentAlive(deployment));

    private Task Reload(Guid engineId)
    {
        _tracker.Reload(engineId);
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
                    _tracker.CheckReporting(_now);
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

    // Answers of many engines arrive close together; they are rendered together instead of one render each.
    private void RequestRender()
    {
        _hasChanges = true;
        if (_isRenderScheduled || _tokenSource.IsCancellationRequested)
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

    private IReadOnlyList<double?> ChainWindow(EngineHostRuntimeModel host)
        => CycleMonitor.Series.Window(host.Id, _now);

    // Explains the host's cycle and, once one was reported, how much of the cycle time it used.
    private static string HostCycleTitle(EngineHostRuntimeModel host, double? latestChainCycle)
        => latestChainCycle is { } latest && host.CycleTime > 0
            ? $"{ClusterOverview.HostCycleHint}: {CycleTimeTexts.Usage(host, latest)}"
            : ClusterOverview.HostCycleHint;

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
}
