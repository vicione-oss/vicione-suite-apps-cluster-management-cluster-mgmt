using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.IoddManagement.Services;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using Sdk.Utils;
using ViciOne.Cluster.Model;
using ViciOne.Ui.Blazor.Components.Grid.Services;
using ViciOne.Ui.Shared.Dx.Components;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
public partial class PackagesControlPanel : IEventConsumer<ClusterPackageChanged>,
    IEventConsumer<ClusterPackageChangeFailed>,
    IEventConsumer<ClusterDependenciesChanged>,
    IEventConsumer<ClusterDependenciesUpdateFailed>
{
    private readonly CancellationTokenSource _tokenSource = new();
    private IQueryable<ClusterPackageInfo> _installedQueryable = Enumerable.Empty<ClusterPackageInfo>().AsQueryable();
    private IQueryable<ClusterPackageInfo> _availableQueryable = Enumerable.Empty<ClusterPackageInfo>().AsQueryable();
    private string _installedFilterText = string.Empty;
    private string _availableFilterText = string.Empty;
    private DxDialog? _refUninstalllPackagesDialog;
    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;
    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] public IPackageService PackageService { get; set; } = null!;
    [Inject] private IMessageBannerService MessageBannerService { get; set; } = default!;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;

    private GridItemSelection<ClusterPackageInfo> InstalledItemSelection { get; } = [];

    private GridItemSelection<ClusterPackageInfo> AvailableItemSelection { get; } = [];

    private string? AdminPolicy { get; }
        = ModulePolicyProvider.GetPolicy<ClusterManagementClientModule>(AccessLevel.Full);

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        InstalledItemSelection.Changed += OnItemSelectionChanged;
        AvailableItemSelection.Changed += OnItemSelectionChanged;

        _subscriptionHandles.Add(Mediator.Register<ClusterPackageChanged>(this));
        _subscriptionHandles.Add(Mediator.Register<ClusterPackageChangeFailed>(this));
        _subscriptionHandles.Add(Mediator.Register<ClusterDependenciesChanged>(this));
        _subscriptionHandles.Add(Mediator.Register<ClusterDependenciesUpdateFailed>(this));

        var allPackages = await PackageService.GetPackages(null, _tokenSource.Token);

        _installedQueryable = allPackages.Where(k => k.InstalledVersion != null || k.RunningClusterVersion != null).AsQueryable();
        _availableQueryable = allPackages.Where(k => string.IsNullOrEmpty(k.InstalledVersion) && string.IsNullOrEmpty(k.RunningClusterVersion)).AsQueryable();
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
            MessageBannerService.ShowMessageBanner(MessageType.Warning, Localization.Common.NeedRestart);
        }
        catch (Exception ex)
        {
            MessageBannerService.ShowMessageBanner(MessageType.Error, ex.Message);
        }
    }

    private async Task OnPackageUploadClickAsync()
        => await JavaScript.InvokeVoidAsync("triggerUploadClick");

    private async Task OnPackageUninstallClickAsync()
        => await _refUninstalllPackagesDialog!.OpenAsync();

    private async Task OnPackageInstallClickAsync()
    {
        var operations = AvailableItemSelection
            .Distinct()
            .Where(item => string.IsNullOrEmpty(item.InstalledVersion) && string.IsNullOrEmpty(item.RunningClusterVersion) && item.Versions?.Count > 0)
            .Select(item => new ClusterDependencyOperation(new ClusterDependency
            {
                Name = item.Name,
                Version = Version.Parse(item.Versions!.First().Version),
            },
            ClusterDependencyOperationKind.Install))
            .ToList();

        if (operations.Count > 0)
        {
            await PackageService.UpdatePackages(operations, _tokenSource.Token);

            MessageBannerService.ShowMessageBanner(MessageType.Warning, Localization.Common.NeedRestart);
        }
    }

    private async Task OnPackageDeleteClickAsync()
    {
        var packagesToDelete = AvailableItemSelection
            .Distinct()
            .Where(item => string.IsNullOrEmpty(item.InstalledVersion) && string.IsNullOrEmpty(item.RunningClusterVersion))
            .ToList();

        foreach (var package in packagesToDelete)
        {
            if (package.Versions is null)
                continue;

            foreach (var version in package.Versions)
            {
                // we don't have it locally so nothing to delete
                if (!version.IsLocal)
                    continue;

                // maybe give user some feedback if nothing to uninstall?        
                await PackageService.DeletePackage(package.Name, version.Version, _tokenSource.Token);
            }
        }
    }

    private async Task OnUninstallPackagesCloseAsync()
        => await _refUninstalllPackagesDialog!.CloseAsync();

    private async Task OnUninstallPackagesOkAsync()
    {
        var items = InstalledItemSelection.Distinct();
        List<ClusterDependencyOperation> operations = [];

        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.InstalledVersion) || !string.IsNullOrEmpty(item.RunningClusterVersion))
                continue;

            operations.Add(new ClusterDependencyOperation(new ClusterDependency
            {
                Name = item.Name,
                Version = Version.Parse(item.InstalledVersion),
            },
            ClusterDependencyOperationKind.Uninstall));
        }

        if (operations.Count > 0)
        {
            await PackageService.UpdatePackages(operations, _tokenSource.Token);

            MessageBannerService.ShowMessageBanner(MessageType.Warning, Localization.Common.NeedRestart);
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
            .Where(i => i.Name.Contains(_installedFilterText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Name);

    private IQueryable<ClusterPackageInfo> FilterAvailableItems(IQueryable<ClusterPackageInfo> packages)
        => packages
            .Where(i => i.Name.Contains(_availableFilterText, StringComparison.OrdinalIgnoreCase))
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

    public async Task Consume(ClientContext<ClusterPackageChanged> context, CancellationToken cancellationToken)
    {
        var name = context.Message.PackageName;

        if (context.Message.Change is ClusterPackageChange.Add or ClusterPackageChange.Update)
        {
            await RefreshPackageAsync(name);
        }
        else if (context.Message.Change == ClusterPackageChange.Delete)
        {
            var package = _availableQueryable.FirstOrDefault(p => p.Name == name);
            if (package is null)
                return;

            // the package version might be deleted but there mmight be other versions
            // only if was a package that was only installed locally we can remove here
            var deletedVersion = package.Versions?.FirstOrDefault(v => v.Version == context.Message.Version);
            if (deletedVersion is null)
                return;

            // we have to update the readonly collection to remove the version
            package.Versions = package.Versions!.Where(v => v.Version != deletedVersion.Version).ToList();
            if (package.Versions.Count > 0)
                return;

            // now we have no versions left and can remove the package entry
            _availableQueryable = _availableQueryable
                .Where(p => p.Name != name)
                .AsQueryable();

            AvailableItemSelection.Remove(p => p.Name == name);
        }

        await InvokeAsync(StateHasChanged);
    }

    public Task Consume(ClientContext<ClusterPackageChangeFailed> context, CancellationToken cancellationToken)
    {
        MessageBannerService.ShowMessageBanner(MessageType.Error, context.Message.Error.Message);

        return Task.CompletedTask;
    }

    public async Task Consume(ClientContext<ClusterDependenciesChanged> context, CancellationToken cancellationToken)
    {
        var hasChanges = false;

        foreach (var operation in context.Message.Changes)
        {
            var name = operation.Operation.Dependency.Name;
            var installed = _installedQueryable.FirstOrDefault(p => p.Name == name);
            if (installed is not null)
            {
                installed.PendingOperation = operation.Action is Sdk.Messaging.CrudAction.Deleted
                    ? null
                    : operation.Operation;
                hasChanges = true;
            }

            var available = _availableQueryable.FirstOrDefault(p => p.Name == name);
            if (available is not null)
            {
                available.PendingOperation = operation.Action is Sdk.Messaging.CrudAction.Deleted
                    ? null
                    : operation.Operation;
                hasChanges = true;
            }
        }

        if (hasChanges)
            await InvokeAsync(StateHasChanged);
    }

    public Task Consume(ClientContext<ClusterDependenciesUpdateFailed> context, CancellationToken cancellationToken)
    {
        MessageBannerService.ShowMessageBanner(MessageType.Error, context.Message.Error.Message);

        return Task.CompletedTask;
    }

    private async Task RefreshPackageAsync(string packageName)
    {
        var refreshed = (await PackageService.GetPackages([packageName], _tokenSource.Token)).FirstOrDefault();
        if (refreshed is null)
            return;

        if (!string.IsNullOrEmpty(refreshed.InstalledVersion))
        {
            var updated = _installedQueryable
            .Where(p => p.Name != packageName)
            .Append(refreshed)
            .OrderBy(p => p.Name)
            .AsQueryable();

            _installedQueryable = updated;
        }
        else
        {
            var updated = _availableQueryable
            .Where(p => p.Name != packageName)
            .Append(refreshed)
            .OrderBy(p => p.Name)
            .AsQueryable();

            _availableQueryable = updated;
        }
    }
}
