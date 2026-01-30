using ClusterManagement.Client.Editor.Services;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using Sdk.Utils;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
public partial class PackageDetailsControlPanel : IEventConsumer<ClusterDependenciesUpdated>, IEventConsumer<ClusterDependenciesUpdateFailed>
{
    private bool _isDetailsExpanded = true;
    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];

    [Inject] private IMessageBannerService MessageBannerService { get; set; } = default!;
    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] private IPackageService PackageService { get; set; } = default!;

    private string? AdminPolicy { get; }
        = ModulePolicyProvider.GetPolicy<ClusterManagementClientModule>(AccessLevel.Full);

    protected override void OnInitialized()
    {
        base.OnInitialized();

        _subscriptionHandles.Add(Mediator.Register<ClusterDependenciesUpdated>(this));
        _subscriptionHandles.Add(Mediator.Register<ClusterDependenciesUpdateFailed>(this));
    }

    public async Task Consume(ClientContext<ClusterDependenciesUpdated> context, CancellationToken cancellationToken)
    {
        var operation = context.Message.Operations.Last(o => o.Dependency.Name == State.PackageInfo?.Name);

        if (operation is null)
            return;

        if (operation.Operation is ClusterDependencyOperationKind.Install)
        {
            State.VersionToInstall = operation.Dependency.Version.ToString();

            var refreshed = (await PackageService.GetPackages([operation.Dependency.Name])).FirstOrDefault();
            if (refreshed is null || State.PackageInfo is null)
                return;

            State.PackageInfo = refreshed;
        }
        else if (operation.Operation is ClusterDependencyOperationKind.Uninstall)
        {
            State.VersionToInstall = string.Empty;

            var refreshed = (await PackageService.GetPackages([operation.Dependency.Name])).FirstOrDefault();
            if (refreshed is null || State.PackageInfo is null)
                return;

            State.PackageInfo = refreshed;
        }

        await InvokeAsync(StateHasChanged);
    }

    public Task Consume(ClientContext<ClusterDependenciesUpdateFailed> context, CancellationToken cancellationToken)
    {
        MessageBannerService.ShowMessageBanner(MessageType.Error, context.Message.Error.Message);

        return Task.CompletedTask;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscriptionHandles.Dispose();

        await base.DisposeAsyncCore();
    }
}
