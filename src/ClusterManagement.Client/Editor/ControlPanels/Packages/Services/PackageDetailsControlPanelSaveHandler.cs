using ClusterManagement.Client.Editor.ControlPanels.Packages.Models;
using ClusterManagement.Internal.Contracts;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages.Services;

public sealed class PackageDetailsControlPanelSaveHandler(IPackageService packageService, IMessageBannerService messageBannerService) : IControlPanelSaveHandler<PackageDetailsControlPanelState>
{
    public async Task<ISaveResult> Save(PackageDetailsControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.PackageInfo is null)
            return new SaveErrorResult("Is empty");

        var dependency = new ClusterDependency
        {
            Name = state.PackageInfo.Name,
            Version = Version.Parse(state.VersionToInstall)
        };

        var operation = new ClusterDependencyOperation(dependency, ClusterDependencyOperationKind.Install);

        var result = await packageService.UpdatePackages([operation], cancellationToken);
        if (result is PackageServiceErrorResult error)
            return new SaveErrorResult(error.ErrorMessage, error.ErrorCode);

        messageBannerService.ShowMessageBanner(MessageType.Warning, Editor.Localization.Common.NeedRestart);

        return new SaveSuccessResult();
    }
}
