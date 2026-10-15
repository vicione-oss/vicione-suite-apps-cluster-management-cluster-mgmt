using System.Globalization;
using ClusterManagement.Client.Management.Extensions;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Public.Events;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace ClusterManagement.Client.Management.Components;

/// <summary>
/// What the running cluster is doing: the version that runs, a deployment in progress with its phase, or a deployment
/// that failed.
/// </summary>
public sealed partial class ClusterStatus : IAsyncDisposable
{
    private readonly CancellationTokenSource _tokenSource = new();
    private DeploymentProgressModel? _deployment;
    private Guid? _lookedUpClusterId;

    [Inject] private IClusterManagementService ManagementService { get; set; } = default!;
    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)] private TimeProvider TimeProvider { get; set; } = default!;
    [Inject] private ILogger<ClusterStatus> Logger { get; set; } = default!;

    /// <summary>
    /// The cluster that runs; <c>null</c> while no version runs, and then a deployment of any cluster is shown.
    /// </summary>
    [Parameter] public Guid? ClusterId { get; set; }

    [Parameter] public Version? RunningVersion { get; set; }

    /// <summary>
    /// <c>true</c> while the page has not yet loaded which version runs; the status then shows no version.
    /// </summary>
    [Parameter] public bool IsLoading { get; set; }

    // The saga reports a failed deployment once it has rolled back, so the version that ran before runs again. A reset
    // deployment did not fail; the deployment panel says what happened to it.
    private string StatusName => _deployment switch
    {
        { Phase: DeploymentPhase.Failed, WasReset: false } when RunningVersion is null => "failed",
        { IsRunning: true } => "deploying",
        _ => RunningVersion is null ? "previous" : "running",
    };

    private string StatusText => _deployment switch
    {
        { Phase: DeploymentPhase.Failed, WasReset: false } failed when RunningVersion is null => Format(ClusterOverview.DeploymentFailed, failed.TargetVersion),
        { IsRunning: true, Phase: DeploymentPhase.Starting } starting => Format(ClusterOverview.DeployingVersion, starting.TargetVersion),
        { IsRunning: true } deploying => $"{Format(ClusterOverview.DeployingVersion, deploying.TargetVersion)} · {deploying.Phase.ToText()}",
        _ => RunningText,
    };

    // A deployment that failed while a version runs gets a chip of its own, so the running version keeps its colour.
    private bool FailedBesideRunning => _deployment is { Phase: DeploymentPhase.Failed, WasReset: false } && RunningVersion is not null;

    private string RunningText => RunningVersion switch
    {
        { } version => Format(ClusterOverview.RunningVersion, version),
        null when IsLoading => "…",
        null => ClusterOverview.NoVersionRunning,
    };

    protected override void OnInitialized()
    {
        ManagementService.OnClusterUpdateStarted += OnClusterUpdateStarted;
        ManagementService.OnClusterUpdateChanged += OnClusterUpdateChanged;
        ManagementService.OnClusterUpdateCompleted += OnClusterUpdateCompleted;
        ManagementService.OnClusterUpdateFailed += OnClusterUpdateFailed;
    }

    // A deployment that already runs when the page opens, or when it shows another cluster, is shown like one that starts later.
    protected override async Task OnParametersSetAsync()
    {
        if (ClusterId is not { } clusterId || clusterId == _lookedUpClusterId)
            return;

        _lookedUpClusterId = clusterId;
        if (_deployment?.ClusterId != clusterId)
            _deployment = null;

        try
        {
            if (await ManagementService.QueryClusterDeploymentState(clusterId, _tokenSource.Token) is { } state)
                _deployment ??= DeploymentProgressModel.Resume(state, null);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogDeploymentStateFailed(Logger, e, clusterId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        ManagementService.OnClusterUpdateStarted -= OnClusterUpdateStarted;
        ManagementService.OnClusterUpdateChanged -= OnClusterUpdateChanged;
        ManagementService.OnClusterUpdateCompleted -= OnClusterUpdateCompleted;
        ManagementService.OnClusterUpdateFailed -= OnClusterUpdateFailed;

        await _tokenSource.CancelAsync();
        _tokenSource.Dispose();
    }

    private async void OnClusterUpdateStarted(object? sender, ClusterUpdateStarted ev)
        => await Update(ev.ClusterId, () => _deployment = DeploymentProgressModel.Start(ev, TimeProvider.GetUtcNow()));

    private async void OnClusterUpdateChanged(object? sender, ClusterUpdateChanged ev)
        => await Update(ev.ClusterId, () => DeploymentFor(ev.ClusterId, ev.TargetVersion)?.Apply(ev));

    // A deployment whose versions cannot be loaded fails before the saga reports its start. A reset ends every deployment
    // of a cluster, also those the status does not show, which leave it alone.
    private async void OnClusterUpdateFailed(object? sender, ClusterUpdateFailed ev)
        => await Update(ev.ClusterId, () =>
        {
            if ((DeploymentFor(ev.ClusterId, ev.CorrelationId) ?? DeploymentFor(ev.ClusterId, ev.Version)) is { } deployment)
                deployment.Fail(ev);
            else if (!ev.WasReset || _deployment is null)
                _deployment = DeploymentProgressModel.FailedBeforeStart(ev);
        });

    // The new version reaches this link through its parameters once the page has loaded it.
    private async void OnClusterUpdateCompleted(object? sender, ClusterUpdateCompleted ev)
        => await Update(ev.ClusterId, () =>
        {
            if (DeploymentFor(ev.ClusterId, ev.NewVersion) is not null)
                _deployment = null;
        });

    private DeploymentProgressModel? DeploymentFor(Guid clusterId, Version version)
        => _deployment?.Matches(clusterId, version) == true ? _deployment : null;

    private DeploymentProgressModel? DeploymentFor(Guid clusterId, Guid correlationId)
        => _deployment?.Matches(clusterId, correlationId) == true ? _deployment : null;

    // The deployment events arrive in async void handlers, where an exception would be lost; the renderer reports it instead.
    private async Task Update(Guid clusterId, Action update)
    {
        try
        {
            await InvokeAsync(() =>
            {
                if (ClusterId is { } shown && shown != clusterId)
                    return;

                update();
                StateHasChanged();
            });
        }
        catch (Exception) when (_tokenSource.IsCancellationRequested)
        {
            // The page was closed while handling the event.
        }
        catch (Exception e)
        {
            await DispatchExceptionAsync(e);
        }
    }

    private static string Format(string format, params object?[] args)
        => string.Format(CultureInfo.CurrentCulture, format, args);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to look up a deployment of cluster {ClusterId} in progress")]
    private static partial void LogDeploymentStateFailed(ILogger logger, Exception exception, Guid clusterId);
}
