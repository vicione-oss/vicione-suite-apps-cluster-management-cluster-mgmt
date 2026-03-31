using ClusterManagement.Client.Editor.ControlPanels.Packages.Services;
using ClusterManagement.Client.Extensions;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using Sdk.Utils;
using ViciOne.Cluster.Model;
using ViciOne.Ui.Blazor.Components.Grid.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
public partial class PackagesControlPanel
{
    private readonly string _uninstallIconCssClasses = @MonochromeIconName.UninstallLight.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly string _updateIconCssClasses = @MonochromeIconName.InstallPendingLight.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly string _uploadIconCssClasses = @MonochromeIconName.Export.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly string _importIconCssClasses = @MonochromeIconName.Import.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly string _deleteIconCssClasses = @MonochromeIconName.Delete.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();

    private readonly CancellationTokenSource _tokenSource = new();
    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;
    [Inject] public IPackageService PackageService { get; set; } = null!;
    [Inject] private IMessageBannerService MessageBannerService { get; set; } = default!;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;

    [Inject(Key = typeof(InstalledPackagesControlPanelServiceKey))] private IGridItemSelection<ClusterPackageInfo> InstalledItemSelection { get; set; } = default!;

    [Inject(Key = typeof(AvailablePackagesControlPanelServiceKey))] private IGridItemSelection<ClusterPackageInfo> AvailableItemSelection { get; set; } = default!;

    private string? AdminPolicy { get; }
        = ModulePolicyProvider.GetPolicy<ClusterManagementClientModule>(AccessLevel.Full);

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscriptionHandles.Dispose();

        PackageService.PackageChanged -= OnPackageChanged;
        PackageService.DependenciesChanged -= OnDependenciesChanged;

        State.Changed -= State_Changed;

        try
        {
            await _tokenSource.CancelAsync();
            _tokenSource.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Ignore: CancellationTokenSource was already disposed.
        }

        if (InstalledItemSelection is not null)
        {
            InstalledItemSelection.Changed -= OnItemSelectionChanged;
            InstalledItemSelection.Clear();
        }

        if (AvailableItemSelection is not null)
        {
            AvailableItemSelection.Changed -= OnItemSelectionChanged;
            AvailableItemSelection.Clear();
        }

        await base.DisposeAsyncCore();
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        InstalledItemSelection.Changed += OnItemSelectionChanged;
        AvailableItemSelection.Changed += OnItemSelectionChanged;

        PackageService.PackageChanged += OnPackageChanged;
        PackageService.DependenciesChanged += OnDependenciesChanged;

        State.Changed += State_Changed;
    }

    private async void State_Changed(ControlPanelStateChangedEventArgs args)
    {
        var stateHasChanged = false;

        if (args.PropertyNames.Contains(nameof(State.AvailableQueryable)))
        {
            AvailableItemSelection.Clear();
            stateHasChanged = true;
        }
        else if (args.PropertyNames.Contains(nameof(State.InstalledQueryable)))
        {
            InstalledItemSelection.Clear();
            stateHasChanged = true;
        }

        if (stateHasChanged)
            await InvokeAsync(StateHasChanged);
    }

    private async void OnItemSelectionChanged(GridItemSelectionChangedEventArgs<ClusterPackageInfo> args)
        => await InvokeAsync(StateHasChanged);

    private async Task OnFileChange(InputFileChangeEventArgs args)
    {
        if (!MessageBannerService.IsValidZipArchive(args))
            return;

        try
        {
            byte[] data;

            await using (var ms = new MemoryStream())
            {
                await args.File.OpenReadStream(ControlPanelConstants.TwoMB).CopyToAsync(ms);
                data = ms.ToArray();
            }

            await PackageService.UploadPackage(data, _tokenSource.Token);
            MessageBannerService.ShowMessageBanner(MessageType.Warning, Editor.Localization.Common.NeedRestart);
        }
        catch (Exception ex)
        {
            MessageBannerService.ShowMessageBanner(MessageType.Error, ex.Message);
        }
    }

    private async Task OnPackageChanged(ClusterPackageChanged change, CancellationToken cancellationToken)
    {
        if (change.Error is not null)
        {
            MessageBannerService.ShowMessageBanner(MessageType.Error, change.Error.Message);
            return;
        }

        var name = change.PackageName;

        if (change.Change is ClusterPackageChange.Add or ClusterPackageChange.Update)
        {
            await RefreshPackageAsync(name);
        }
        else if (change.Change == ClusterPackageChange.Delete)
        {
            if (!State.DeletePackage(name, change.Version))
                return;

            AvailableItemSelection.Remove(p => p.Name == name);
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnDependenciesChanged(ClusterDependenciesChanged change, CancellationToken token)
    {
        if (change.Error is not null)
        {
            MessageBannerService.ShowMessageBanner(MessageType.Error, change.Error.Message);
            return;
        }

        var hasChanges = State.ApplyChanges(change.Changes);
        if (hasChanges)
            await InvokeAsync(StateHasChanged);
    }

    private async Task OnPackageUploadClickAsync()
        => await JavaScript.InvokeVoidAsync("triggerUploadClick");

    private bool CanUninstallPackages()
        => InstalledItemSelection.Count != 0
        && InstalledItemSelection.All(i => !string.IsNullOrEmpty(i.InstalledVersion)
        && string.IsNullOrEmpty(i.RunningClusterVersion));

    private async Task UninstallPackagesClick()
    {
        var items = InstalledItemSelection.Distinct();

        State.UpdateUninstallOperations(items);

        if (State.HasPendingChanges())
        {
            await BeginEdit();
        }
    }

    private bool CanUpdatePackages()
        => InstalledItemSelection.Count != 0
        && InstalledItemSelection.All(i => !string.IsNullOrEmpty(i.InstalledVersion)
        && !string.IsNullOrEmpty(i.AvailableUpdateVersion)
        && i.InstalledVersion != i.AvailableUpdateVersion);

    private async Task UpdatePackagesClick()
    {
        var items = InstalledItemSelection.Distinct();

        State.UpdateUpdateOperations(items);

        if (State.HasPendingChanges())
        {
            await BeginEdit();
        }
    }

    private bool CanInstallAvailablePackages()
        => AvailableItemSelection.Count != 0
        && AvailableItemSelection.All(i => string.IsNullOrEmpty(i.InstalledVersion) && string.IsNullOrEmpty(i.RunningClusterVersion));

    private async Task InstallAvailablePackagesClick()
    {
        var packagesToInstall = AvailableItemSelection
            .Distinct()
            .Where(item => string.IsNullOrEmpty(item.InstalledVersion) && string.IsNullOrEmpty(item.RunningClusterVersion) && item.Versions?.Count > 0);

        State.UpdateInstallOperations(packagesToInstall);

        if (State.HasPendingChanges())
        {
            await BeginEdit();
        }
    }

    private bool CanDeleteAvailablePackages()
        => AvailableItemSelection.Count != 0
        && AvailableItemSelection.All(i => string.IsNullOrEmpty(i.InstalledVersion)
            && string.IsNullOrEmpty(i.RunningClusterVersion)
            && i.Versions?.Any(v => v.IsLocal) == true);

    private async Task DeleteAvailablePackagesClick()
    {
        var packagesToDelete = AvailableItemSelection
            .Distinct()
            .Where(item => string.IsNullOrEmpty(item.InstalledVersion) && string.IsNullOrEmpty(item.RunningClusterVersion))
            .ToList();

        State.UpdatePackagesToBeDeleted(packagesToDelete);

        if (State.HasPendingChanges())
        {
            await BeginEdit();
        }
    }

    private bool CanResetInstalledSelectedModules()
        => InstalledItemSelection.Count != 0
        && InstalledItemSelection.All(k => k.PendingOperation?.OperationKind == ClusterDependencyOperationKind.Uninstall);

    private async Task ResetInstalledSelectedModules()
    {
        var operations = InstalledItemSelection
            .Where(k => k.PendingOperation != null && k.PendingOperation.OperationKind == ClusterDependencyOperationKind.Uninstall)
            .Distinct()
            .Select(item => new ClusterDependencyOperation(new ClusterDependency
            {
                Name = item.Name,
                Version = item.PendingOperation!.Dependency.Version,
            },
            ClusterDependencyOperationKind.Install))
            .ToList();

        State.UpdateUninstallOperations(operations);

        if (State.HasPendingChanges())
        {
            await BeginEdit();
        }
    }

    private bool CanResetAvailableSelectedModules()
        => AvailableItemSelection.Count != 0
        && AvailableItemSelection.All(k => k.PendingOperation?.OperationKind == ClusterDependencyOperationKind.Install);

    private async Task ResetAvailableSelectedModules()
    {
        var operations = AvailableItemSelection
            .Where(k => k.PendingOperation != null && k.PendingOperation.OperationKind == ClusterDependencyOperationKind.Install)
            .Distinct()
            .Select(item => new ClusterDependencyOperation(new ClusterDependency
            {
                Name = item.Name,
                Version = item.PendingOperation!.Dependency.Version,
            },
            ClusterDependencyOperationKind.Uninstall))
            .ToList();

        State.UpdateUninstallOperations(operations);

        if (State.HasPendingChanges())
        {
            await BeginEdit();
        }
    }

    private async Task ShowDetailsAsync(ClusterPackageInfo item)
    {
        var result = await ControlPanelRequest.Send<PackageDetailsControlPanel, PackageDetailsControlPanelState>(s =>
        {
            s.PackageInfo = item;
        });
    }

    private IQueryable<ClusterPackageInfo> FilterInstalledItems(IQueryable<ClusterPackageInfo> packages)
    {
        if (string.IsNullOrEmpty(State.InstalledFilterText))
            return packages.OrderBy(d => d.Name);

        return packages
                .Where(i => i.Name.Contains(State.InstalledFilterText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Name);
    }

    private IQueryable<ClusterPackageInfo> FilterAvailableItems(IQueryable<ClusterPackageInfo> packages)
    {
        if (string.IsNullOrEmpty(State.AvailableFilterText))
            return packages.OrderBy(d => d.Name);

        return packages
                .Where(i => i.Name.Contains(State.AvailableFilterText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Name);
    }

    private static string GetLatestVersion(ClusterPackageInfo? package)
        => package?.Versions?.FirstOrDefault()?.Version ?? string.Empty;

    private async Task RefreshPackageAsync(string packageName)
    {
        var refreshed = (await PackageService.GetPackages([packageName], _tokenSource.Token)).FirstOrDefault();
        if (refreshed is null)
            return;

        State.UpdatePackage(refreshed);
    }
}
