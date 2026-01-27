using ClusterManagement.Client.Editor.ControlPanels.Packages;
using Sdk.Client.ControlPanels.Services;

namespace ClusterManagement.Client.Editor.Services;

public sealed class PackageDetailsControlPanelResetHandler : IControlPanelResetHandler<PackageDetailsControlPanelState>
{
    public Task Reset(PackageDetailsControlPanelState state, CancellationToken cancellationToken)
    {
        state.VersionToInstall = state.PackageInfo?.InstalledVersion ?? string.Empty;

        return Task.CompletedTask;
    }
}
