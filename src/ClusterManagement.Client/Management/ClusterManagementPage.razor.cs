using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Logging;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Public.Events;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using ViciOne.Ui.Blazor.Components.Button.Enums;
using ViciOne.Ui.Blazor.Components.Button.Extensions;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace ClusterManagement.Client.Management;

public sealed partial class ClusterManagementPage : IEventConsumer<ClusterChanged>
{
    private readonly string _deployIconCssClass = MonochromeIconName.SendLight.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _editIconCssClass = MonochromeIconName.Edit.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _infoIconCssClass = MonochromeIconName.InfoLight.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _downloadIconCssClass = MonochromeIconName.Export.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _deleteIconCssClass = MonochromeIconName.Delete.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();

    private readonly CancellationTokenSource _tokenSource = new();

    private bool _hasLoaded;
    private IDisposable? _subscriptionHandle;
    private string? _sagaState;

    // the table only applies a new list instance, so SelectedCluster always replaces it
    private List<ClusterInfoModel> _selectedClusters = [];

    [Inject] private IUiMediator Mediator { get; set; } = default!;

    [Inject] private IClusterManagementService ManagementService { get; set; } = default!;

    [Inject] private NavigationManager NavManager { get; set; } = default!;

    private List<ClusterInfoModel> Clusters { get; } = [];

    private ClusterInfoModel? SelectedCluster
    {
        get;
        set
        {
            field = value;
            _selectedClusters = value is null ? [] : [value];
        }
    }

    protected override string PageTitle => Common.ModuleTitle;

    protected override async ValueTask DisposeInternal()
    {
        _subscriptionHandle?.Dispose();

        try
        {
            await _tokenSource.CancelAsync();
            _tokenSource.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Ignore: CancellationTokenSource was already disposed.
        }

        ManagementService.OnClusterUpdateStarted -= OnClusterUpdateStarted;
        ManagementService.OnClusterUpdateChanged -= OnClusterUpdateChanged;
        ManagementService.OnClusterUpdateCompleted -= OnClusterUpdateCompleted;
        ManagementService.OnClusterUpdateFailed -= OnClusterUpdateFailed;

        await base.DisposeInternal();
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await ReloadClusterList();

        _hasLoaded = true;
        _subscriptionHandle = Mediator.Register(this);

        //https://learn.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming
        ManagementService.OnClusterUpdateStarted += OnClusterUpdateStarted;
        ManagementService.OnClusterUpdateChanged += OnClusterUpdateChanged;
        ManagementService.OnClusterUpdateCompleted += OnClusterUpdateCompleted;
        ManagementService.OnClusterUpdateFailed += OnClusterUpdateFailed;

        await UpdateCurrentDeploymentsState();
    }

    private async Task ReloadClusterList()
    {
        Clusters.Clear();

        var versions = await ManagementService.QueryClusterVersions(null, _tokenSource.Token);
        if (versions.Count != 0)
            Clusters.AddRange(versions);

        SelectedCluster = Clusters.FirstOrDefault();
    }

    private async Task UpdateCurrentDeploymentsState()
    {
        var clusterIds = Clusters.GroupBy(k => k.Id).Select(k => k.Key).ToArray();

        foreach (var clusterId in clusterIds)
        {
            var state = await ManagementService.QueryClusterDeploymentState(clusterId, _tokenSource.Token);
            if (state is null)
                continue;

            // If we have already a running state we don't override it
            var model = Clusters.FirstOrDefault(k => k.IsMatch(state.ClusterId, state.TargetVersion));
            if (model is null || !string.IsNullOrEmpty(model.Status))
                return;

            // We just display the saga state as status because there'll be a redesign soon
            model.Status = state.CurrentState;
            model.IsExecuting = true;
        }

        if (clusterIds.Length > 0)
            await InvokeAsync(StateHasChanged);
    }

    private void OnSelectedItemsChanged(List<ClusterInfoModel> selectedItems)
    {
        SelectedCluster = selectedItems.FirstOrDefault();
        _selectedClusters = selectedItems;
    }

    private async void OnClusterUpdateStarted(object? sender, ClusterUpdateStarted ev)
    {
        var model = Clusters.FirstOrDefault(k => k.IsMatch(ev.ClusterId, ev.TargetVersion));
        if (model is null)
            return;

        model.Status = Common.StateStarted;
        model.IsExecuting = true;

        await InvokeAsync(StateHasChanged);
    }

    private async void OnClusterUpdateChanged(object? sender, ClusterUpdateChanged ev)
    {
        var model = Clusters.FirstOrDefault(k => k.IsMatch(ev.ClusterId, ev.TargetVersion));
        if (model is null)
            return;

        model.Status = ev.CurrentState ?? Common.StateUpdating;
        model.IsExecuting = true;
        model.DeploymentDuration = null;
        model.DeploymentErrors.Clear();

        if (ev.ApplicationId is not null && ev.Error is not null)
        {
            if (model.DeploymentErrors.All(k => k.ApplicationId != ev.ApplicationId))
            {
                model.DeploymentErrors.Add(new DeploymentErrorModel(ev.ApplicationId.Value, ev.Error.ErrorCode, ev.Error.Message));
            }
        }

        await InvokeAsync(StateHasChanged);
    }

    private async void OnClusterUpdateCompleted(object? sender, ClusterUpdateCompleted ev)
    {
        var model = Clusters.FirstOrDefault(k => k.IsMatch(ev.ClusterId, ev.NewVersion));
        if (model is null)
            return;

        // reset all clusters
        foreach (var cluster in Clusters.Where(k => Equals(k.Id, ev.ClusterId)))
        {
            cluster.IsActive = false;
            cluster.Status = "";
        }

        // set status on current
        model.Status = Common.StateActive;
        model.IsActive = true;
        model.IsExecuting = false;
        model.DeploymentDuration = ev.Duration;
        model.DeploymentErrors.Clear();

        await InvokeAsync(StateHasChanged);
    }

    private async void OnClusterUpdateFailed(object? sender, ClusterUpdateFailed ev)
    {
        var model = Clusters.FirstOrDefault(k => k.IsMatch(ev.ClusterId, ev.Version));
        if (model is null)
            return;

        model.Status = Common.StateFailed;
        model.IsExecuting = false;
        model.DeploymentErrors.AddRange(ev.ErrorsByApplication.Select(k => new DeploymentErrorModel(k.Key, k.Value.ErrorCode, k.Value.Message)));

        await InvokeAsync(StateHasChanged);
    }

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

    public async Task Consume(ClientContext<ClusterChanged> context, CancellationToken cancellationToken)
    {
        if (context.Message.Action == CrudAction.Deleted)
        {
            var deleted = Clusters.FirstOrDefault(k => k.IsMatch(context.Message.ClusterId, context.Message.Version));
            if (deleted is null)
                return;

            Clusters.Remove(deleted);
            SelectedCluster = null;

            // set state if latest item was deleted
            var latest = Clusters.FirstOrDefault();
            latest?.IsLatest = true;
        }
        else
        {
            await ReloadClusterList();
        }

        await InvokeAsync(StateHasChanged);
    }

    private void HandleEdit(ClusterInfoModel model)
    {
        if (!model.IsLatest)
            return;

        NavManager.NavigateToEditor(model);
    }

    private async Task ResetSagaStates()
    {
        try
        {
            var clusterIds = Clusters.GroupBy(k => k.Id).Select(k => k.Key);
            if (clusterIds is null)
                return;

            foreach (var clusterId in clusterIds)
            {
                await ManagementService.ResetSagaState(clusterId, _tokenSource.Token);
            }

            foreach (var cluster in Clusters.Where(k => !k.IsActive))
            {
                cluster.Status = string.Empty;
                cluster.IsExecuting = false;
            }
        }
        catch (Exception ex)
        {
            Logger.LogFailure(ex, nameof(ResetSagaStates));
            _sagaState = ex.Message;
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task GetSagaState()
    {
        try
        {
            _sagaState = null;

            var clusterIds = Clusters.GroupBy(k => k.Id).Select(k => k.Key);
            if (clusterIds is null)
                return;

            foreach (var clusterId in clusterIds)
            {
                var state = await ManagementService.QueryClusterDeploymentState(clusterId, _tokenSource.Token);
                if (state is null)
                    continue;

                _sagaState += $"Cluster {clusterId} - State: {state.CurrentState}, ActiveVersion: {state.ActiveVersion}, TargetVersion: {state.TargetVersion}\n";
            }
        }
        catch (Exception ex)
        {
            Logger.LogFailure(ex, nameof(GetSagaState));
            _sagaState = ex.Message;
        }

        await InvokeAsync(StateHasChanged);
    }

    private void NavigateToEvents()
        => NavManager.NavigateToManagementEvents();

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
