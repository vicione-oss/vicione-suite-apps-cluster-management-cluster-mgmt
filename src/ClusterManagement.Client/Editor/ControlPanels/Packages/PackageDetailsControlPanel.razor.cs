using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using Sdk.Messaging;
using Sdk.Utils;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages;

[ControlPanelCategory<ControlPanelCategoryDescriptor>]
public partial class PackageDetailsControlPanel : IEventConsumer<ClusterDependenciesChanged>, IEventConsumer<ClusterDependenciesUpdateFailed>
{
    private bool _isDetailsExpanded = true;
    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];

    [Inject] private IMessageBannerService MessageBannerService { get; set; } = default!;
    [Inject] private IUiMediator Mediator { get; set; } = default!;

    private string? AdminPolicy { get; }
        = ModulePolicyProvider.GetPolicy<ClusterManagementClientModule>(AccessLevel.Full);

    protected override void OnInitialized()
    {
        base.OnInitialized();

        _subscriptionHandles.Add(Mediator.Register<ClusterDependenciesChanged>(this));
        _subscriptionHandles.Add(Mediator.Register<ClusterDependenciesUpdateFailed>(this));
    }

    public async Task Consume(ClientContext<ClusterDependenciesChanged> context, CancellationToken cancellationToken)
    {
        var change = context.Message.Changes.Last(o => o.Operation.Dependency.Name == State.PackageInfo?.Name);
        if (change is null)
            return;

        if (State.PackageInfo is null)
            return;

        if (change.Action is CrudAction.Deleted)
        {
            State.PackageInfo.PendingOperation = null;
            State.VersionToInstall = string.Empty;
        }
        else if (change.Operation.OperationKind is ClusterDependencyOperationKind.Install)
        {
            State.VersionToInstall = change.Operation.Dependency.Version.ToString();
            State.PackageInfo.PendingOperation = change.Operation;
        }
        else if (change.Operation.OperationKind is ClusterDependencyOperationKind.Uninstall)
        {
            State.VersionToInstall = string.Empty;
            State.PackageInfo.PendingOperation = change.Operation;
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
