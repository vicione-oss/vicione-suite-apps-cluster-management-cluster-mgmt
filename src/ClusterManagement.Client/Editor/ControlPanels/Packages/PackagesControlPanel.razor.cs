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
    IEventConsumer<ClusterDependenciesUpdated>,
    IEventConsumer<ClusterDependenciesUpdateFailed>
{
    private IQueryable<ClusterPackageInfo> _packageInfosQueryable = Enumerable.Empty<ClusterPackageInfo>().AsQueryable();
    private string _filterText = string.Empty;
    private DxDialog? _refDeletePackagesDialog;
    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];

    [Inject] private IControlPanelRequest ControlPanelRequest { get; set; } = default!;
    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] public IPackageService PackageService { get; set; } = null!;
    [Inject] private IMessageBannerService MessageBannerService { get; set; } = default!;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;

    private GridItemSelection<ClusterPackageInfo> ItemSelection { get; } = [];

    private string? AdminPolicy { get; }
        = ModulePolicyProvider.GetPolicy<ClusterManagementClientModule>(AccessLevel.Full);

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        ItemSelection.Changed += OnItemSelectionChanged;

        _subscriptionHandles.Add(Mediator.Register<ClusterPackageChanged>(this));
        _subscriptionHandles.Add(Mediator.Register<ClusterPackageChangeFailed>(this));
        _subscriptionHandles.Add(Mediator.Register<ClusterDependenciesUpdated>(this));
        _subscriptionHandles.Add(Mediator.Register<ClusterDependenciesUpdateFailed>(this));

        _packageInfosQueryable = (await PackageService.GetPackages()).AsQueryable();
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

            await PackageService.UploadPackage(data);
            MessageBannerService.ShowMessageBanner(MessageType.Warning, Localization.Common.NeedRestart);
        }
        catch (Exception ex)
        {
            MessageBannerService.ShowMessageBanner(MessageType.Error, ex.Message);
        }
    }


    private async Task OnPackageUploadClickAsync()
        => await JavaScript.InvokeVoidAsync("triggerUploadClick");

    private async Task OnPackageDeleteClickAsync()
        => await _refDeletePackagesDialog!.OpenAsync();

    private async Task OnDeletePackagesCloseAsync()
        => await _refDeletePackagesDialog!.CloseAsync();

    private async Task OnDeletePackagesOkAsync()
    {
        var items = ItemSelection.Distinct();
        List<ClusterDependencyOperation> operations = [];

        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.InstalledVersion))
                continue;

            operations.Add(new ClusterDependencyOperation(new ClusterDependency
            {
                Name = item.Name,
                Version = Version.Parse(item.InstalledVersion),
            },
            ClusterDependencyOperationKind.Uninstall));
        }

        await PackageService.UpdatePackages(operations);

        MessageBannerService.ShowMessageBanner(MessageType.Warning, Localization.Common.NeedRestart);

        await _refDeletePackagesDialog!.CloseAsync();
    }

    private async Task ShowDetailsAsync(ClusterPackageInfo item)
    {
        var result = await ControlPanelRequest.Send<PackageDetailsControlPanel, PackageDetailsControlPanelState>(s =>
        {
            s.PackageInfo = item;
        });
    }

    private IQueryable<ClusterPackageInfo> FilterItems(IQueryable<ClusterPackageInfo> devices)
        => devices
            .Where(i => i.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Name);

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscriptionHandles.Dispose();

        if (ItemSelection is not null)
        {
            ItemSelection.Changed -= OnItemSelectionChanged;
            ItemSelection.Clear();
        }

        await base.DisposeAsyncCore();
    }

    public async Task Consume(ClientContext<ClusterPackageChanged> context, CancellationToken cancellationToken)
    {
        var name = context.Message.PackageName;

        if (context.Message.Change is ClusterPackageChange.Add or ClusterPackageChange.Update)
        {
            var packages = await PackageService.GetPackages();
            var packagesList = new List<ClusterPackageInfo>(packages)
            {
                new()
                {
                    Name = name,
                    Description = "N/A",
                    InstalledVersion = string.Empty,
                    Versions = []
                }
            };

            _packageInfosQueryable = packagesList.AsQueryable();
        }
        else if (context.Message.Change == ClusterPackageChange.Delete)
        {
            var packages = (await PackageService.GetPackages()).Where(p => p.Name != name);
            _packageInfosQueryable = packages.AsQueryable();

            ItemSelection.Remove(p => p.Name == name);
        }

        await InvokeAsync(StateHasChanged);
    }

    public Task Consume(ClientContext<ClusterPackageChangeFailed> context, CancellationToken cancellationToken)
    {
        MessageBannerService.ShowMessageBanner(MessageType.Error, context.Message.Error.Message);

        return Task.CompletedTask;
    }

    public async Task Consume(ClientContext<ClusterDependenciesUpdated> context, CancellationToken cancellationToken)
    {
        foreach (var operation in context.Message.Operations)
        {
            if (operation.Operation is ClusterDependencyOperationKind.Install)
            {
                var packages = await PackageService.GetPackages();
                packages.First(p => p.Name == operation.Dependency.Name).InstalledVersion = operation.Dependency.Version.ToString();
            }
            else if (operation.Operation is ClusterDependencyOperationKind.Uninstall)
            {
                _packageInfosQueryable.First(p => p.Name == operation.Dependency.Name).InstalledVersion = string.Empty;

                ItemSelection.Remove(p => p.Name == operation.Dependency.Name);
            }
        }

        await InvokeAsync(StateHasChanged);
    }

    public Task Consume(ClientContext<ClusterDependenciesUpdateFailed> context, CancellationToken cancellationToken)
    {
        MessageBannerService.ShowMessageBanner(MessageType.Error, context.Message.Error.Message);

        return Task.CompletedTask;
    }
}
