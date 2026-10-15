using System.Globalization;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Blazor.Components.Button.Enums;
using ViciOne.Ui.Blazor.Components.Button.Extensions;
using ViciOne.Ui.Blazor.Components.Dialog.Components;
using ViciOne.Ui.Localization.Resources;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace ClusterManagement.Client.Management.Components;

public sealed partial class ClusterVersionList
{
    private const int CollapsedCount = 8;

    private readonly string _deployIconCssClass = IconCssClass(MonochromeIconName.SendLight);
    private readonly string _editIconCssClass = IconCssClass(MonochromeIconName.Edit);
    private readonly string _downloadIconCssClass = IconCssClass(MonochromeIconName.Export);
    private readonly string _deleteIconCssClass = IconCssClass(MonochromeIconName.Delete);

    private Dialog? _refDefaultDeployDialog;
    private Dialog? _refDeleteDialog;
    private ClusterInfoModel? _pendingDelete;
    private bool _showAll;

    [Parameter, EditorRequired] public IReadOnlyList<ClusterInfoModel> Versions { get; set; } = [];
    [Parameter] public DeploymentProgressModel? Deployment { get; set; }
    [Parameter] public EventCallback<ClusterInfoModel> OnDeploy { get; set; }
    [Parameter] public EventCallback<ClusterInfoModel> OnEdit { get; set; }
    [Parameter] public EventCallback<ClusterInfoModel> OnDownload { get; set; }
    [Parameter] public EventCallback<ClusterInfoModel> OnDelete { get; set; }

    private bool IsDeploying => Deployment?.IsRunning == true;

    // The versions can belong to several clusters; the default deployed is the one of the cluster that runs.
    private ClusterInfoModel? DefaultVersion
        => Versions.FirstOrDefault(v => v.IsDefault && (ActiveVersion is not { } active || v.Id == active.Id));

    private ClusterInfoModel? ActiveVersion => Versions.FirstOrDefault(v => v.IsActive);

    // Clusters can share a name, e.g. a second "Default Cluster"; their versions are headed by the cluster they belong to,
    // which names the cluster instead of each row.
    private bool HasSeveralClusters => Versions.Select(v => v.Id).Distinct().Skip(1).Any();

    private bool CanDeployDefault => DefaultVersion is { IsActive: false } && !IsDeploying;

    private string DefaultDeployQuestion => string.Format(CultureInfo.CurrentCulture, ClusterOverview.DeployDefaultConfirmation, DefaultVersion?.Version);

    private Version? RunningVersion => ActiveVersion?.Version;

    /// <summary>
    /// The newest versions, plus the running one and the one being deployed when they are older than those.
    /// </summary>
    internal IReadOnlyList<ClusterInfoModel> VisibleVersions
    {
        get
        {
            if (_showAll || Versions.Count <= CollapsedCount)
                return Versions;

            return [.. Versions.Where((version, index) => index < CollapsedCount
                || version.IsActive
                || Deployment?.Matches(version.Id, version.Version) == true)];
        }
    }

    /// <summary>
    /// How many versions the collapsed list skips right before <paramref name="visibleIndex"/>.
    /// </summary>
    private int HiddenBefore(IReadOnlyList<ClusterInfoModel> visible, int visibleIndex)
    {
        if (visibleIndex == 0)
            return 0;

        return IndexInVersions(visible[visibleIndex]) - IndexInVersions(visible[visibleIndex - 1]) - 1;
    }

    /// <summary>
    /// How many versions the collapsed list skips after its last row.
    /// </summary>
    private int HiddenAfter(IReadOnlyList<ClusterInfoModel> visible)
        => visible.Count == 0 ? 0 : Versions.Count - 1 - IndexInVersions(visible[^1]);

    private int IndexInVersions(ClusterInfoModel version)
    {
        for (var i = 0; i < Versions.Count; i++)
        {
            if (Versions[i] == version)
                return i;
        }

        return -1;
    }

    private async Task OnDefaultDeployShowAsync()
        => await _refDefaultDeployDialog!.ShowAsync();

    private async Task OnDefaultDeployCloseAsync()
        => await _refDefaultDeployDialog!.CloseAsync();

    private async Task OnDefaultDeployOkAsync()
    {
        await _refDefaultDeployDialog!.CloseAsync();

        if (DefaultVersion is { } defaultVersion)
            await OnDeploy.InvokeAsync(defaultVersion);
    }

    private async Task OnDeleteShowAsync(ClusterInfoModel version)
    {
        _pendingDelete = version;
        await _refDeleteDialog!.ShowAsync();
    }

    private async Task OnDeleteCloseAsync()
        => await _refDeleteDialog!.CloseAsync();

    private async Task OnDeleteOkAsync()
    {
        await _refDeleteDialog!.CloseAsync();

        if (_pendingDelete is { } version)
            await OnDelete.InvokeAsync(version);
    }

    private static string StatusName(ClusterVersionStatus status) => status switch
    {
        ClusterVersionStatus.Running => "running",
        ClusterVersionStatus.Deploying => "deploying",
        ClusterVersionStatus.Failed => "failed",
        ClusterVersionStatus.Draft => "draft",
        ClusterVersionStatus.Default => "default",
        _ => "previous",
    };

    private static string StatusText(ClusterVersionStatus status) => status switch
    {
        ClusterVersionStatus.Running => ClusterOverview.Running,
        ClusterVersionStatus.Deploying => ClusterOverview.VersionDeploying,
        ClusterVersionStatus.Failed => ClusterOverview.VersionFailed,
        ClusterVersionStatus.Draft => ClusterOverview.VersionDraft,
        ClusterVersionStatus.Default => CommonVocabulary.Default,
        _ => ClusterOverview.VersionPrevious,
    };

    private static string IconCssClass(MonochromeIconName icon)
        => icon.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
}
