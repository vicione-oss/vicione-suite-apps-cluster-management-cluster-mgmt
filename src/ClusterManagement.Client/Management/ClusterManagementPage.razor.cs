using System.Globalization;
using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Logging;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Client.Services;
using ClusterManagement.Internal.Events;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using ViciOne.Cluster.Model;
using ViciOne.Ui.Blazor.Components.Button.Enums;
using ViciOne.Ui.Blazor.Components.Button.Extensions;
using ViciOne.Ui.Blazor.Components.Dialog.Components;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace ClusterManagement.Client.Management;

public sealed partial class ClusterManagementPage : IEventConsumer<ClusterChanged>
{
    private readonly CancellationTokenSource _tokenSource = new();
    private readonly string _deployIconCssClass = MonochromeIconName.SendLight.GetCssClasses(ButtonSize.Medium.ToMonochromeIconSize()).ToSpaceSeparated();

    private bool _hasLoaded;
    private IDisposable? _subscriptionHandle;
    private Dialog? _refSagaStateDialog;
    private Dialog? _refResetSagaStateDialog;
    private string? _sagaError;
    private List<DeploymentState> _sagaStates = [];
    private Cluster? _activeCluster;
    private ClusterTopologySummary? _summary;
    private DeploymentProgressModel? _deployment;

    // The cluster the reset dialog asks about; a deployment that starts or ends while it is open does not change it.
    private Guid? _resetClusterId;

    // The right is looked up once for each authentication state, not in every render.
    private Task<AuthenticationState>? _checkedAuthenticationState;
    private bool _mayReset;

    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] private IClusterManagementService ManagementService { get; set; } = default!;
    [Inject] private IMessageBannerService MessageBannerService { get; set; } = default!;
    [Inject] private NavigationManager NavManager { get; set; } = default!;
    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)] private TimeProvider TimeProvider { get; set; } = default!;
    [Inject] private ExpertViewState ExpertView { get; set; } = default!;
    [Inject] private IAuthorizationService AuthorizationService { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState>? AuthenticationState { get; set; }

    private List<ClusterInfoModel> Versions { get; set; } = [];

    private ClusterInfoModel? ActiveVersion => Versions.FirstOrDefault(v => v.IsActive);

    // The versions list every cluster; the page bar deploys the newest version of the one that runs.
    private ClusterInfoModel? DraftVersion => Versions.FirstOrDefault(v => v.IsLatest && !v.IsActive && (ActiveVersion is null || v.Id == ActiveVersion.Id));

    protected override string PageTitle => Common.ModuleTitle;

    // A draft whose deployment just failed is not announced as ready; the failure says what happened to it.
    private Version? ReadyDraftVersion => _deployment is { Phase: DeploymentPhase.Failed } failed && failed.TargetVersion == DraftVersion?.Version
        ? null
        : DraftVersion?.Version;

    // A deployment of another cluster leaves the engines of the running one alone.
    private Guid? RunningClusterDeployment => _deployment is { IsRunning: true } deployment && deployment.ClusterId == _activeCluster?.Id
        ? deployment.CorrelationId
        : null;

    // The deployment panel shows a deployment, or else the running version; its reset is about that cluster.
    private Guid? PanelClusterId => _deployment?.ClusterId ?? ActiveVersion?.Id;

    private string? ResetClusterName => Versions.FirstOrDefault(v => v.Id == _resetClusterId)?.DisplayName;

    private string DeployText => DraftVersion is { } draft
        ? string.Format(CultureInfo.CurrentCulture, ClusterOverview.DeployVersion, draft.Version)
        : ClusterOverview.Deploy;

    protected override async ValueTask DisposeInternal()
    {
        _subscriptionHandle?.Dispose();
        ExpertView.Changed -= OnExpertViewChanged;

        ManagementService.OnClusterUpdateStarted -= OnClusterUpdateStarted;
        ManagementService.OnClusterUpdateChanged -= OnClusterUpdateChanged;
        ManagementService.OnClusterUpdateCompleted -= OnClusterUpdateCompleted;
        ManagementService.OnClusterUpdateFailed -= OnClusterUpdateFailed;
        ManagementService.OnClusterUpdateRejected -= OnClusterUpdateRejected;
        ManagementService.OnClusterNodeDeploymentStepChanged -= OnClusterNodeDeploymentStepChanged;
        ManagementService.OnDeploymentStateReset -= OnDeploymentStateReset;

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
        ExpertView.Changed += OnExpertViewChanged;
        ManagementService.OnClusterUpdateStarted += OnClusterUpdateStarted;
        ManagementService.OnClusterUpdateChanged += OnClusterUpdateChanged;
        ManagementService.OnClusterUpdateCompleted += OnClusterUpdateCompleted;
        ManagementService.OnClusterUpdateFailed += OnClusterUpdateFailed;
        ManagementService.OnClusterUpdateRejected += OnClusterUpdateRejected;
        ManagementService.OnClusterNodeDeploymentStepChanged += OnClusterNodeDeploymentStepChanged;
        ManagementService.OnDeploymentStateReset += OnDeploymentStateReset;

        await ReloadVersions();
        await ResumeRunningDeployment();

        _hasLoaded = true;
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (AuthenticationState == _checkedAuthenticationState)
            return;

        _checkedAuthenticationState = AuthenticationState;
        _mayReset = await MayReset();
    }

    public async Task Consume(ClientContext<ClusterChanged> context, CancellationToken cancellationToken)
        => await InvokeAsync(ReloadVersionsAndRender);

    private void OnExpertViewChanged()
        => _ = InvokeAsync(StateHasChanged);

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

            // A version being committed is not stored yet; the ClusterUpdateStarted after the commit brings its names.
            var target = DeploymentPhases.FromSagaState(state.CurrentState) == DeploymentPhase.Committing
                ? null
                : await QueryTargetCluster(state.ClusterId, state.TargetVersion);
            var deployment = Named(DeploymentProgressModel.Resume(state, target));
            if (target is not null)
                deployment.KnowNamesOf(target);

            _deployment ??= deployment;
            return;
        }
    }

    // Without the target version the deployment is still shown, only without its instances and the names of what only
    // the target version holds.
    private async Task<Cluster?> QueryTargetCluster(Guid clusterId, Version targetVersion)
    {
        try
        {
            return await ManagementService.QueryCluster(clusterId, targetVersion, _tokenSource.Token);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Logger.LogFailure(e, nameof(QueryTargetCluster));
            return null;
        }
    }

    private async void OnClusterUpdateStarted(object? sender, ClusterUpdateStarted ev)
        => await HandleEvent(async () =>
        {
            var deployment = _deployment = Named(DeploymentProgressModel.Start(ev, TimeProvider.GetUtcNow()));
            StateHasChanged();

            // Only the rollback needs the names of the target version, so the steps that arrive meanwhile do without them.
            if (await QueryTargetCluster(ev.ClusterId, ev.TargetVersion) is { } target)
                deployment.KnowNamesOf(target);
        });

    // The running version is the one the deployment replaces, so it names what the update removes.
    private DeploymentProgressModel Named(DeploymentProgressModel deployment)
    {
        if (_activeCluster is { } running)
            deployment.KnowNamesOf(running);
        return deployment;
    }

    private async void OnClusterUpdateChanged(object? sender, ClusterUpdateChanged ev)
        => await UpdateDeployment(() => DeploymentFor(ev.ClusterId, ev.TargetVersion)?.Apply(ev));

    // A deployment whose versions cannot be loaded fails before the saga reports its start. A reset ends every deployment
    // of a cluster, also those the panel does not show, which leave the panel alone.
    private async void OnClusterUpdateFailed(object? sender, ClusterUpdateFailed ev)
        => await UpdateDeployment(() =>
        {
            if ((DeploymentFor(ev.ClusterId, ev.CorrelationId) ?? DeploymentFor(ev.ClusterId, ev.Version)) is { } deployment)
                deployment.Fail(ev);
            else if (!ev.WasReset || _deployment is null)
                _deployment = DeploymentProgressModel.FailedBeforeStart(ev);
        });

    private async void OnClusterUpdateCompleted(object? sender, ClusterUpdateCompleted ev)
        => await HandleEvent(async () =>
        {
            DeploymentFor(ev.ClusterId, ev.NewVersion)?.Complete(ev);
            await ReloadVersions();
            StateHasChanged();
        });

    private async void OnClusterNodeDeploymentStepChanged(object? sender, ClusterNodeDeploymentStepChanged ev)
        => await UpdateDeployment(() => DeploymentFor(ev.ClusterId, ev.CorrelationId)?.Apply(ev, TimeProvider.GetUtcNow()));

    private async void OnClusterUpdateRejected(object? sender, ClusterUpdateRejected ev)
        => await HandleEvent(() =>
        {
            MessageBannerService.ShowMessageBanner(MessageType.Warning,
                string.Format(CultureInfo.CurrentCulture, ClusterOverview.DeploymentRejected, ev.RejectedVersion, ev.TargetVersion));
            return Task.CompletedTask;
        });

    // Each reset deployment ends as failed through ClusterUpdateFailed, which also updates the deployment panel.
    private async void OnDeploymentStateReset(object? sender, DeploymentStateReset ev)
        => await HandleEvent(() =>
        {
            MessageBannerService.ShowMessageBanner(ev.Error is null ? MessageType.Information : MessageType.Error, ResetText(ev));
            return Task.CompletedTask;
        });

    private static string ResetText(DeploymentStateReset reset)
        => reset switch
        {
            { Error: { } error } => string.Format(CultureInfo.CurrentCulture, ClusterOverview.SagaStateNotReset, error.Message),
            { Reset.Count: 0 } => ClusterOverview.NothingToReset,
            _ => string.Format(CultureInfo.CurrentCulture, ClusterOverview.SagaStateReset, string.Join(", ", reset.Reset.Select(s => $"v{s.TargetVersion}"))),
        };

    private DeploymentProgressModel? DeploymentFor(Guid clusterId, Version version)
        => _deployment?.Matches(clusterId, version) == true ? _deployment : null;

    private DeploymentProgressModel? DeploymentFor(Guid clusterId, Guid correlationId)
        => _deployment?.Matches(clusterId, correlationId) == true ? _deployment : null;

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

    private async Task OnResetSagaStateShowAsync()
    {
        _resetClusterId = PanelClusterId;
        await _refResetSagaStateDialog!.ShowAsync();
    }

    private async Task OnResetSagaStateCloseAsync()
        => await _refResetSagaStateDialog!.CloseAsync();

    private async Task OnResetSagaStateOkAsync()
    {
        await _refResetSagaStateDialog!.CloseAsync();
        await ResetSagaStates();
    }

    private async Task ResetSagaStates()
    {
        if (_resetClusterId is not { } clusterId || !await MayReset())
            return;

        try
        {
            await ManagementService.ResetSagaState(clusterId, await UserName(), _tokenSource.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogFailure(ex, nameof(ResetSagaStates));
            MessageBannerService.ShowMessageBanner(MessageType.Error,
                string.Format(CultureInfo.CurrentCulture, ClusterOverview.SagaStateNotReset, ex.Message));
        }
    }

    // The button only renders for those allowed; the check repeats before the reset, so a stale page cannot reset on a
    // lost right.
    private Task<bool> MayReset()
        => EngineControlRight.IsGranted(AuthorizationService, AuthenticationState);

    private async Task<string> UserName()
        => AuthenticationState is { } authenticationState ? (await authenticationState).User.Identity?.Name ?? string.Empty : string.Empty;

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
