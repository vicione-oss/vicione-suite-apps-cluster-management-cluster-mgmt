using System.Globalization;
using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Logging;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using ViciOne.Cluster.Model;
using ViciOne.Ui.Blazor.Components.Dialog.Components;

namespace ClusterManagement.Client.Management;

public sealed partial class ClusterManagementPage : IEventConsumer<ClusterChanged>
{
    private readonly CancellationTokenSource _tokenSource = new();

    private bool _hasLoaded;
    private IDisposable? _subscriptionHandle;
    private Dialog? _refSagaStateDialog;
    private string? _sagaError;
    private List<DeploymentState> _sagaStates = [];
    private Cluster? _activeCluster;
    private ClusterTopologySummary? _summary;
    private DeploymentProgressModel? _deployment;

    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] private IClusterManagementService ManagementService { get; set; } = default!;
    [Inject] private IMessageBannerService MessageBannerService { get; set; } = default!;
    [Inject] private NavigationManager NavManager { get; set; } = default!;
    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)] private TimeProvider TimeProvider { get; set; } = default!;

    private List<ClusterInfoModel> Versions { get; set; } = [];

    private ClusterInfoModel? ActiveVersion => Versions.FirstOrDefault(v => v.IsActive);

    // The versions list every cluster; the header deploys the newest version of the one that runs.
    private ClusterInfoModel? DraftVersion => Versions.FirstOrDefault(v => v.IsLatest && !v.IsActive && (ActiveVersion is null || v.Id == ActiveVersion.Id));

    protected override string PageTitle => Common.ModuleTitle;

    protected override async ValueTask DisposeInternal()
    {
        _subscriptionHandle?.Dispose();

        ManagementService.OnClusterUpdateStarted -= OnClusterUpdateStarted;
        ManagementService.OnClusterUpdateChanged -= OnClusterUpdateChanged;
        ManagementService.OnClusterUpdateCompleted -= OnClusterUpdateCompleted;
        ManagementService.OnClusterUpdateFailed -= OnClusterUpdateFailed;
        ManagementService.OnClusterUpdateRejected -= OnClusterUpdateRejected;

        try
        {
            await _tokenSource.CancelAsync();
            _tokenSource.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Ignore: CancellationTokenSource was already disposed.
        }

        await base.DisposeInternal();
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // Subscribed before loading, so a deployment that starts meanwhile is seen through its events.
        _subscriptionHandle = Mediator.Register(this);
        ManagementService.OnClusterUpdateStarted += OnClusterUpdateStarted;
        ManagementService.OnClusterUpdateChanged += OnClusterUpdateChanged;
        ManagementService.OnClusterUpdateCompleted += OnClusterUpdateCompleted;
        ManagementService.OnClusterUpdateFailed += OnClusterUpdateFailed;
        ManagementService.OnClusterUpdateRejected += OnClusterUpdateRejected;

        await ReloadVersions();
        await ResumeRunningDeployment();

        _hasLoaded = true;
    }

    public async Task Consume(ClientContext<ClusterChanged> context, CancellationToken cancellationToken)
        => await InvokeAsync(ReloadVersionsAndRender);

    private async Task ReloadVersionsAndRender()
    {
        await ReloadVersions();
        StateHasChanged();
    }

    private async Task ReloadVersions()
    {
        Versions = await ManagementService.QueryClusterVersions(null, _tokenSource.Token);
        await ReloadActiveCluster();
    }

    private async Task ReloadActiveCluster()
    {
        var active = ActiveVersion;
        if (active is null || (_activeCluster?.Id == active.Id && _activeCluster.Version == active.Version))
            return;

        try
        {
            _activeCluster = await ManagementService.QueryCluster(active.Id, active.Version, _tokenSource.Token);
            _summary = ClusterTopologySummary.Create(_activeCluster);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The previous version's cluster must not be shown as the running one; the next reload tries again.
            _activeCluster = null;
            _summary = null;
            Logger.LogFailure(e, nameof(ReloadActiveCluster));
        }
    }

    // Every cluster is asked, also one without a running version: its first deployment may be in progress.
    // A deployment whose events arrived while loading is newer than any queried state and is kept.
    private async Task ResumeRunningDeployment()
    {
        foreach (var clusterId in Versions.Select(v => v.Id).Distinct())
        {
            var state = await ManagementService.QueryClusterDeploymentState(clusterId, _tokenSource.Token);
            if (state is null)
                continue;

            var target = await QueryTargetCluster(state);
            _deployment ??= DeploymentProgressModel.Resume(state, target);
            return;
        }
    }

    // Without the target version the deployment is still shown, only without its instances.
    private async Task<Cluster?> QueryTargetCluster(DeploymentState state)
    {
        try
        {
            return await ManagementService.QueryCluster(state.ClusterId, state.TargetVersion, _tokenSource.Token);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Logger.LogFailure(e, nameof(QueryTargetCluster));
            return null;
        }
    }

    private async void OnClusterUpdateStarted(object? sender, ClusterUpdateStarted ev)
        => await UpdateDeployment(() => _deployment = DeploymentProgressModel.Start(ev, TimeProvider.GetUtcNow()));

    private async void OnClusterUpdateChanged(object? sender, ClusterUpdateChanged ev)
        => await UpdateDeployment(() => DeploymentFor(ev.ClusterId, ev.TargetVersion)?.Apply(ev));

    // A deployment whose versions cannot be loaded fails before the saga reports its start.
    private async void OnClusterUpdateFailed(object? sender, ClusterUpdateFailed ev)
        => await UpdateDeployment(() =>
        {
            if (DeploymentFor(ev.ClusterId, ev.Version) is { } deployment)
                deployment.Fail(ev);
            else
                _deployment = DeploymentProgressModel.FailedBeforeStart(ev);
        });

    private async void OnClusterUpdateCompleted(object? sender, ClusterUpdateCompleted ev)
        => await HandleEvent(async () =>
        {
            DeploymentFor(ev.ClusterId, ev.NewVersion)?.Complete(ev);
            await ReloadVersions();
            StateHasChanged();
        });

    private async void OnClusterUpdateRejected(object? sender, ClusterUpdateRejected ev)
        => await HandleEvent(() =>
        {
            MessageBannerService.ShowMessageBanner(MessageType.Warning,
                string.Format(CultureInfo.CurrentCulture, ClusterOverview.DeploymentRejected, ev.RejectedVersion, ev.TargetVersion));
            return Task.CompletedTask;
        });

    private DeploymentProgressModel? DeploymentFor(Guid clusterId, Version version)
        => _deployment?.Matches(clusterId, version) == true ? _deployment : null;

    private Task UpdateDeployment(Action update)
        => HandleEvent(() =>
        {
            update();
            StateHasChanged();
            return Task.CompletedTask;
        });

    // The deployment events arrive in async void handlers, where an exception would be lost; the renderer reports it instead.
    private async Task HandleEvent(Func<Task> handle)
    {
        try
        {
            await InvokeAsync(handle);
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

    private Task DeployDraft()
        => DraftVersion is { } draft ? DeployClusterVersion(draft) : Task.CompletedTask;

    private async Task DeployClusterVersion(ClusterInfoModel model)
    {
        try
        {
            await ManagementService.DeployCluster(model.Id, model.Version, _tokenSource.Token);
        }
        catch (Exception e)
        {
            Logger.LogFailure(e, nameof(DeployClusterVersion));
        }
    }

    private async Task RemoveCluster(ClusterInfoModel model)
    {
        try
        {
            await ManagementService.RemoveCluster(model.Id, model.Version, _tokenSource.Token);
        }
        catch (Exception e)
        {
            Logger.LogFailure(e, nameof(RemoveCluster));
        }
    }

    private void OpenEditor()
    {
        if (Versions.FirstOrDefault(v => v.IsLatest) is { } latest)
            NavManager.NavigateToEditor(latest);
    }

    private void HandleEdit(ClusterInfoModel model)
    {
        if (model.IsLatest)
            NavManager.NavigateToEditor(model);
    }

    private async Task ResetSagaStates()
    {
        try
        {
            foreach (var clusterId in Versions.Select(v => v.Id).Distinct())
                await ManagementService.ResetSagaState(clusterId, _tokenSource.Token);

            _deployment = null;
        }
        catch (Exception ex)
        {
            Logger.LogFailure(ex, nameof(ResetSagaStates));
            _sagaError = ex.Message;
            _sagaStates = [];
            await _refSagaStateDialog!.ShowAsync();
        }
    }

    private async Task ShowSagaState()
    {
        _sagaError = null;
        _sagaStates = [];

        try
        {
            foreach (var clusterId in Versions.Select(v => v.Id).Distinct())
            {
                if (await ManagementService.QueryClusterDeploymentState(clusterId, _tokenSource.Token) is { } state)
                    _sagaStates.Add(state);
            }
        }
        catch (Exception ex)
        {
            Logger.LogFailure(ex, nameof(ShowSagaState));
            _sagaError = ex.Message;
        }

        await _refSagaStateDialog!.ShowAsync();
    }

    private async Task OnSagaStateCloseAsync()
        => await _refSagaStateDialog!.CloseAsync();

    private async Task DownloadClusterJson(ClusterInfoModel model)
    {
        try
        {
            var cluster = await ManagementService.QueryCluster(model.Id, model.Version, _tokenSource.Token);

            await JsInterop.DownloadClusterJson(cluster);
        }
        catch (Exception e)
        {
            Logger.LogDownloadClusterJsonFailure(e, model.DisplayName, model.Version);
        }
    }
}
