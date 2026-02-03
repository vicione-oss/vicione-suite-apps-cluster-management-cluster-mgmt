using ClusterManagement.Client.Editor.ControlPanels.Packages;
using ClusterManagement.Client.Editor.Localization;
using ClusterManagement.Internal.Contracts;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;

namespace ClusterManagement.Client.Editor.Services;

public sealed class PackageDetailsControlPanelSaveHandler(IPackageService packageService, IMessageBannerService messageBannerService) : IControlPanelSaveHandler<PackageDetailsControlPanelState>
{
    public async Task<ISaveResult> Save(PackageDetailsControlPanelState state, CancellationToken cancellationToken)
    {
        try
        {
            if (state.PackageInfo is null)
                return new SaveErrorResult("Is empty");

            await packageService.UpdatePackages([
                new(new() { Name = state.PackageInfo.Name, Version = Version.Parse(state.VersionToInstall)}, ClusterDependencyOperationKind.Install)
            ], cancellationToken);

            messageBannerService.ShowMessageBanner(MessageType.Warning, Common.NeedRestart);

            return new SaveSuccessResult();
        }
        catch (Exception e)
        {
            return new SaveErrorResult(e.Message);
        }
    }
}
