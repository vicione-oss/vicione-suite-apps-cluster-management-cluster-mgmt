using System.Globalization;
using ClusterManagement.Client.Management.Extensions;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Client.Services;
using ClusterManagement.Internal.Commands;
using ClusterManagement.Public.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Utils;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.DebugPages;

/// <summary>
/// The engines of the running version, grouped by engine host: their state and cycle times for everyone, and in the
/// Expert view their details, packages and, with the Engine control feature, starting, stopping and tearing down.
/// </summary>
public sealed partial class DebugEnginesPage : IEventConsumer<ClusterChanged>, IEventConsumer<EngineStarted>,
    IEventConsumer<EngineStopped>, IEventConsumer<EngineTornDown>
{
    private static readonly TimeSpan s_refreshInterval = TimeSpan.FromSeconds(1);

    // The answers of many engines arrive close together; they are rendered together instead of one render each.
    private static readonly TimeSpan s_renderDelay = TimeSpan.FromMilliseconds(100);

    private readonly CancellationTokenSource _tokenSource = new();
    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];

    private EngineStateTracker _tracker = default!;
    private Cluster? _cluster;
    private IReadOnlyDictionary<Guid, string> _instanceNames = new Dictionary<Guid, string>();
    private IReadOnlyDictionary<Guid, EngineDetails> _engineDetails = new Dictionary<Guid, EngineDetails>();
    private DateTimeOffset _now;
    private bool _hasLoaded;
    private bool _isRenderScheduled;

    // A newer load of the running version wins over a slower earlier one.
    private int _loadGeneration;

    private EngineDialogs? _dialogs;

    // The right is looked up once for each authentication state, not for every engine in every render.
    private Task<AuthenticationState>? _checkedAuthenticationState;
    private bool _mayControlEngines;

    [Inject] private IUiMediator UiMediator { get; set; } = default!;
    [Inject] private IClusterManagementService ManagementService { get; set; } = default!;
    [Inject] private IClusterService ClusterService { get; set; } = default!;
    [Inject] private EngineCycleMonitor CycleMonitor { get; set; } = default!;
    [Inject] private ExpertViewState ExpertView { get; set; } = default!;
    [Inject] private IAuthorizationService AuthorizationService { get; set; } = default!;
    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)] private TimeProvider TimeProvider { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState>? AuthenticationState { get; set; }

    // The top bar keeps the app's name; the page bar says which page of it this is.
    protected override string PageTitle => Management.Localization.Common.ModuleTitle;

    private bool Expert => ExpertView.IsExpert;

    // One for each <col> of the table.
    private int ColumnCount => Expert ? 9 : 3;

    private IReadOnlyList<EngineHostRuntimeModel> SortedHosts()
        => [.. _tracker.Hosts.OrderBy(host => host.Name, StringComparer.CurrentCultureIgnoreCase)];

    protected override async Task OnInitializedAsync()
    {
        // Before the first await, so the page never renders without it.
        _now = TimeProvider.GetUtcNow();
        _tracker = new EngineStateTracker(UiMediator, CycleMonitor, TimeProvider, Logger, InvokeAsync, RequestRender);

        await base.OnInitializedAsync();

        ExpertView.Changed += OnExpertViewChanged;
        ManagementService.OnClusterUpdateStarted += OnDeploymentStarted;
        ManagementService.OnClusterUpdateCompleted += OnDeploymentCompleted;
        ManagementService.OnClusterUpdateFailed += OnDeploymentFailed;
        ManagementService.OnClusterUpdateChanged += OnDeploymentChanged;
        ManagementService.OnClusterNodeDeploymentStepChanged += OnDeploymentStepChanged;
        _subscriptionHandle.Add(UiMediator.Register<ClusterChanged>(this));
        _subscriptionHandle.Add(UiMediator.Register<EngineStarted>(this));
        _subscriptionHandle.Add(UiMediator.Register<EngineStopped>(this));
        _subscriptionHandle.Add(UiMediator.Register<EngineTornDown>(this));

        // Loading waits until every engine has answered, which takes up to the state timeout when one does not.
        await CheckEngineControl();
        await LoadRunningVersion();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        await CheckEngineControl();
    }

    private async Task CheckEngineControl()
    {
        if (AuthenticationState == _checkedAuthenticationState)
            return;

        _checkedAuthenticationState = AuthenticationState;
        _mayControlEngines = await MayControlEngines();
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
            _ = RefreshPeriodically(_tokenSource.Token);
    }

    protected override async ValueTask DisposeInternal()
    {
        ExpertView.Changed -= OnExpertViewChanged;
        ManagementService.OnClusterUpdateStarted -= OnDeploymentStarted;
        ManagementService.OnClusterUpdateCompleted -= OnDeploymentCompleted;
        ManagementService.OnClusterUpdateFailed -= OnDeploymentFailed;
        ManagementService.OnClusterUpdateChanged -= OnDeploymentChanged;
        ManagementService.OnClusterNodeDeploymentStepChanged -= OnDeploymentStepChanged;
        _subscriptionHandle.Dispose();

        try
        {
            await _tokenSource.CancelAsync();
            _tokenSource.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Ignore: CancellationTokenSource was already disposed.
        }

        if (_tracker is not null)
            await _tracker.DisposeAsync();

        await base.DisposeInternal();
    }

    // A change of the clusters may be a new running version; the engines of the same version keep their states.
    public Task Consume(ClientContext<ClusterChanged> context, CancellationToken cancellationToken)
    {
        _ = InvokeAsync(LoadRunningVersion);
        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<EngineStarted> context, CancellationToken cancellationToken)
        => Reload(context.Message.EngineId);

    public Task Consume(ClientContext<EngineStopped> context, CancellationToken cancellationToken)
        => Reload(context.Message.EngineId);

    public Task Consume(ClientContext<EngineTornDown> context, CancellationToken cancellationToken)
        => Reload(context.Message.EngineId);

    private Task Reload(Guid engineId)
    {
        _tracker.Reload(engineId);
        return Task.CompletedTask;
    }

    private async Task LoadRunningVersion()
    {
        var generation = ++_loadGeneration;
        try
        {
            var running = (await ManagementService.QueryClusterVersions(null, _tokenSource.Token)).FirstOrDefault(c => c.IsActive);
            if (running is not null && _cluster is { } shown && shown.Id == running.Id && shown.Version == running.Version)
                return;

            var cluster = running is null ? null : await ClusterService.Query(running.Id, running.Version, _tokenSource.Token);
            if (generation != _loadGeneration)
                return;

            // A page opened during a deployment waits for its end, like one that sees it start.
            var deployment = cluster is null ? null : (await ManagementService.QueryClusterDeploymentState(cluster.Id, _tokenSource.Token))?.CorrelationId;
            if (generation != _loadGeneration)
                return;

            Show(cluster);
            if (cluster is not null)
                await _tracker.Track(EngineHostRuntimeModel.FromCluster(cluster), deployment);
        }
        catch (Exception) when (_tokenSource.IsCancellationRequested)
        {
            // The page was closed while loading.
        }
        catch (Exception e)
        {
            LogLoadFailed(Logger, e);
            _hasLoaded = true;
            StateHasChanged();
        }
    }

    private void Show(Cluster? cluster)
    {
        _cluster = cluster;
        _instanceNames = cluster is null ? [] : EngineHostRuntimeModel.InstanceNamesOf(cluster);
        _engineDetails = cluster is null ? [] : EngineDetails.Of(cluster);
        _hasLoaded = true;
        StateHasChanged();
    }

    private async Task RefreshPeriodically(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(s_refreshInterval, TimeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await InvokeAsync(() =>
                {
                    _now = TimeProvider.GetUtcNow();
                    _tracker.CheckReporting(_now);
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException)
        {
            // The page was closed.
        }
    }

    // A deployment of another cluster leaves the engines shown alone.
    private void OnDeploymentStarted(object? sender, ClusterUpdateStarted e)
        => _ = InvokeAsync(() => IsShown(e.ClusterId) ? _tracker.SetDeployment(e.CorrelationId) : Task.CompletedTask);

    private void OnDeploymentCompleted(object? sender, ClusterUpdateCompleted e)
        => _ = InvokeAsync(() => EndDeployment(e.CorrelationId));

    private void OnDeploymentFailed(object? sender, ClusterUpdateFailed e)
        => _ = InvokeAsync(() => EndDeployment(e.CorrelationId));

    private void OnDeploymentChanged(object? sender, ClusterUpdateChanged e)
        => _ = InvokeAsync(() => _tracker.DeploymentAlive(e.CorrelationId));

    private void OnDeploymentStepChanged(object? sender, ClusterNodeDeploymentStepChanged e)
        => _ = InvokeAsync(() => _tracker.DeploymentAlive(e.CorrelationId));

    // A completed deployment runs another version, whose engines are loaded; a failed one rolled back to the version
    // shown, whose engines are asked again.
    private async Task EndDeployment(Guid deployment)
    {
        await LoadRunningVersion();
        await _tracker.EndDeployment(deployment);
    }

    private bool IsShown(Guid clusterId)
        => _cluster?.Id == clusterId;

    private void RequestRender()
    {
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
            // The page was closed.
        }
    }

    private void OnExpertViewChanged()
        => _ = InvokeAsync(StateHasChanged);

    private IEnumerable<EngineRuntimeModel> EnginesInOrder(EngineHostRuntimeModel host)
        => host.Engines
            .OrderBy(engine => DetailsOf(engine).RunIndex)
            .ThenBy(engine => engine.EngineName, StringComparer.CurrentCultureIgnoreCase);

    private EngineDetails DetailsOf(EngineRuntimeModel engine)
        => _engineDetails.GetValueOrDefault(engine.EngineId, EngineDetails.Unknown);

    private string InstanceName(EngineHostRuntimeModel host)
        => _instanceNames.GetValueOrDefault(host.InstanceId, host.InstanceId.ToString());

    private bool IsOverCycleTime(EngineHostRuntimeModel host)
        => host.IsOverCycleTime(_tracker.Series.Window(host.Id, _now));

    private double? LatestCycle(EngineRuntimeModel engine)
        => engine.DeploymentId is { } deploymentId ? _tracker.Series.Latest(deploymentId, _now) : null;

    private CycleRange? RangeOf(Guid source)
        => _tracker.Series.Range(source);

    private string RangeHint(CycleRange range)
        => CycleTimeTexts.RangeHint(range, TimeProvider.LocalTimeZone);

    private static string HostCycleTitle(bool isOverCycleTime)
        => isOverCycleTime ? ClusterOverview.CycleTimeExceeded : ClusterOverview.HostCycleHint;

    private static string HostCycleText(EngineHostRuntimeModel host, double? latestChainCycle)
        => latestChainCycle is { } latest && host.CycleTime > 0
            ? CycleTimeTexts.Usage(host, latest)
            : string.Format(CultureInfo.CurrentCulture, ClusterOverview.HostCycleTime, host.CycleTime);

    private async Task StartEngine(EngineRuntimeModel engine)
    {
        if (await MayControlEngines())
            await UiMediator.Send<StartEngineOnInstance>(new(engine.EngineId), engine.InstanceId);
    }

    private async Task StopEngine(EngineRuntimeModel engine)
    {
        if (await MayControlEngines())
            await UiMediator.Send<StopEngineOnInstance>(new(engine.EngineId), engine.InstanceId);
    }

    private Task AskToTearDown(EngineRuntimeModel engine)
        => _dialogs!.AskToTearDown(engine);

    // The question may stay open while the engine starts or a deployment begins; the engine host would tear down a
    // running engine as well.
    private async Task TearDownEngine(EngineRuntimeModel engine)
    {
        if (CurrentStateOf(engine) is { IsStopped: true } current && !_tracker.IsDeploying && await MayControlEngines())
            await UiMediator.Send<TearDownEngineOnInstance>(new(current.EngineId), current.InstanceId);
    }

    private EngineRuntimeModel? CurrentStateOf(EngineRuntimeModel engine)
        => _tracker.Hosts.SelectMany(host => host.Engines).FirstOrDefault(known => known.EngineId == engine.EngineId);

    // The buttons only render for those allowed; the check repeats here, so a stale page cannot act on a lost right.
    private Task<bool> MayControlEngines()
        => EngineControlRight.IsGranted(AuthorizationService, AuthenticationState);

    private Task ShowPackages(EngineRuntimeModel engine)
        => _dialogs!.ShowPackages(engine, _tokenSource.Token);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load the engines of the running version")]
    private static partial void LogLoadFailed(ILogger logger, Exception exception);
}
