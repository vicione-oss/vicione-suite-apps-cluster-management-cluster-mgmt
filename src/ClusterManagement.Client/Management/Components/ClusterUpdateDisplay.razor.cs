using ClusterManagement.Client.Management.Extensions;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Client.Management.Services;
using ClusterManagement.Public.Events;
using Microsoft.AspNetCore.Components;

namespace ClusterManagement.Client.Management.Components;

public sealed partial class ClusterUpdateDisplay : IDisposable
{
    private ClusterUpdateStatusModel? UpdateStatus { get; set; }

    [Inject] private IClusterManagementService ManagementService { get; set; } = default!;

    public void Dispose()
    {
        ManagementService.OnClusterUpdateStarted -= OnClusterUpdateStarted;
        ManagementService.OnClusterUpdateChanged -= OnClusterUpdateChanged;
        ManagementService.OnClusterUpdateCompleted -= OnClusterUpdateCompleted;
        ManagementService.OnClusterUpdateFailed -= OnClusterUpdateFailed;
    }

    protected override void OnInitialized()
    {
        ManagementService.OnClusterUpdateStarted += OnClusterUpdateStarted;
        ManagementService.OnClusterUpdateChanged += OnClusterUpdateChanged;
        ManagementService.OnClusterUpdateCompleted += OnClusterUpdateCompleted;
        ManagementService.OnClusterUpdateFailed += OnClusterUpdateFailed;
    }

    private async void OnClusterUpdateStarted(object? sender, ClusterUpdateStarted ev)
    {
        UpdateStatus = new ClusterUpdateStatusModel(ev);

        await InvokeAsync(StateHasChanged);
    }

    private async void OnClusterUpdateChanged(object? sender, ClusterUpdateChanged ev)
    {
        if (UpdateStatus?.ClusterId != ev.ClusterId)
            return;

        if (ev.NodeId != null)
        {
            UpdateStatus.ProcessNodeChange(ev.NodeId.Value, ev.Error);
        }

        if (ev.ApplicationId is not null)
        {
            UpdateStatus.ProcessApplicationChange(ev.ApplicationId.Value, ev.Error);
        }

        await InvokeAsync(StateHasChanged);
    }

    private async void OnClusterUpdateCompleted(object? sender, ClusterUpdateCompleted ev)
        => await InvokeAsync(StateHasChanged);

    private async void OnClusterUpdateFailed(object? sender, ClusterUpdateFailed ev)
    {
        if (UpdateStatus?.ClusterId != ev.ClusterId)
            return;

        if (ev.ErrorsByApplication is null)
            return;

        foreach (var appError in ev.ErrorsByApplication)
        {
            UpdateStatus.ProcessApplicationChange(appError.Key, appError.Value);
        }

        await InvokeAsync(StateHasChanged);
    }

    private static string GetUpdateModelCss(ClusterUpdateModel model)
    {
        var cssClasses = new List<string>();
        if (model is ClusterUpdateNodeModel)
            cssClasses.Add("node");
        else if (model is ClusterUpdateApplicationModel)
            cssClasses.Add("application");

        if (model.Processed)
        {
            if (model.Error is not null)
                cssClasses.Add("fail");
            else
                cssClasses.Add("success");
        }

        return string.Join(' ', cssClasses);
    }
}
