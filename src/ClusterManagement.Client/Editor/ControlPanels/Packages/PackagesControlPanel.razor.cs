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
using ViciOne.Ui.Blazor.Components.Grid.Services;
using ViciOne.Ui.Shared.Dx.Components;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
public partial class PackagesControlPanel
{
    private readonly CancellationTokenSource _tokenSource = new();
    private DxDialog? _refUninstalllPackagesDialog;
    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;
    [Inject] public IPackageService PackageService { get; set; } = null!;
    [Inject] private IMessageBannerService MessageBannerService { get; set; } = default!;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;

    [Inject(Key = typeof(PackagesControlPanelServiceKey))] private IGridItemSelection<ClusterPackageInfo> InstalledItemSelection { get; set; } = default!;

    [Inject(Key = typeof(PackagesControlPanelServiceKey))] private IGridItemSelection<ClusterPackageInfo> AvailableItemSelection { get; set; } = default!;

    private string? AdminPolicy { get; }
        = ModulePolicyProvider.GetPolicy<ClusterManagementClientModule>(AccessLevel.Full);

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        InstalledItemSelection.Changed += OnItemSelectionChanged;
        AvailableItemSelection.Changed += OnItemSelectionChanged;

        PackageService.PackageChanged += OnPackageChanged;
        PackageService.DependenciesChanged += OnDependenciesChanged;
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

    private async Task OnPackageUninstallClickAsync()
        => await _refUninstalllPackagesDialog!.OpenAsync();

    private async Task OnPackageInstallClickAsync()
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

    private async Task OnPackageDeleteClickAsync()
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

    private async Task OnUninstallPackagesCloseAsync()
        => await _refUninstalllPackagesDialog!.CloseAsync();

    private async Task OnUninstallPackagesOkAsync()
    {
        var items = InstalledItemSelection.Distinct();

        State.UpdateUninstallOperations(items);

        if (State.HasPendingChanges())
        {
            await BeginEdit();
        }

        // maybe give user some feedback if nothing to uninstall?        
        await _refUninstalllPackagesDialog!.CloseAsync();
    }

    private async Task ShowDetailsAsync(ClusterPackageInfo item)
    {
        var result = await ControlPanelRequest.Send<PackageDetailsControlPanel, PackageDetailsControlPanelState>(s =>
        {
            s.PackageInfo = item;
        });
    }

    private IQueryable<ClusterPackageInfo> FilterInstalledItems(IQueryable<ClusterPackageInfo> packages)
        => packages
            .Where(i => i.Name.Contains(State.InstalledFilterText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Name);

    private IQueryable<ClusterPackageInfo> FilterAvailableItems(IQueryable<ClusterPackageInfo> packages)
        => packages
            .Where(i => i.Name.Contains(State.AvailableFilterText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Name);

    private bool CanInstallAvailablePackages()
        => AvailableItemSelection.Count != 0
        && AvailableItemSelection.All(i => string.IsNullOrEmpty(i.InstalledVersion) && string.IsNullOrEmpty(i.RunningClusterVersion));

    private bool CanDeleteAvailablePackages()
        => AvailableItemSelection.Count != 0
        && AvailableItemSelection.All(i => string.IsNullOrEmpty(i.InstalledVersion)
            && string.IsNullOrEmpty(i.RunningClusterVersion)
            && i.Versions?.Any(v => v.IsLocal) == true);

    private static string GetLatestVersion(ClusterPackageInfo? package)
        => package?.Versions?.FirstOrDefault()?.Version ?? string.Empty;

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscriptionHandles.Dispose();

        PackageService.PackageChanged -= OnPackageChanged;
        PackageService.DependenciesChanged -= OnDependenciesChanged;

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

    private async Task RefreshPackageAsync(string packageName)
    {
        var refreshed = (await PackageService.GetPackages([packageName], _tokenSource.Token)).FirstOrDefault();
        if (refreshed is null)
            return;

        State.UpdatePackage(refreshed);
    }
}
