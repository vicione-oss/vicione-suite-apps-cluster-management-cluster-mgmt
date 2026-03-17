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
public partial class PackageDetailsControlPanel :
    IEventConsumer<ClusterDependenciesChanged>
{
    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];

    [Inject] private IMessageBannerService MessageBannerService { get; set; } = default!;
    [Inject] private IUiMediator Mediator { get; set; } = default!;

    private string? AdminPolicy { get; }
        = ModulePolicyProvider.GetPolicy<ClusterManagementClientModule>(AccessLevel.Full);

    protected override void OnInitialized()
    {
        base.OnInitialized();

        _subscriptionHandles.Add(Mediator.Register(this));
    }

    public async Task Consume(ClientContext<ClusterDependenciesChanged> context, CancellationToken cancellationToken)
    {
        if (context.Message.Error is not null)
        {
            MessageBannerService.ShowMessageBanner(MessageType.Error, context.Message.Error.Message);
            return;
        }

        var change = context.Message.Changes.Last(o => o.Operation.Dependency.Name == State.PackageInfo?.Name);
        if (change is null)
            return;

        var changed = State.UpdatePackage(change);
        if (changed)
            await InvokeAsync(StateHasChanged);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscriptionHandles.Dispose();

        await base.DisposeAsyncCore();
    }
}
