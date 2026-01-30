using ClusterManagement.Client.Editor.ControlPanels.Packages;
using Sdk.Client.ControlPanels.Services;

namespace ClusterManagement.Client.Editor.Services;

public sealed class PackageDetailsControlPanelResetHandler : IControlPanelResetHandler<PackageDetailsControlPanelState>
{
    public Task Reset(PackageDetailsControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.PackageInfo is null)
            return Task.CompletedTask;

        state.VersionToInstall = state.PackageInfo.PendingOperation?.Dependency.Version.ToString() ?? state.PackageInfo.InstalledVersion ?? string.Empty;

        if (state.PackageInfo.PendingOperation?.Operation is Internal.Contracts.ClusterDependencyOperationKind.Uninstall)
            state.VersionToInstall = string.Empty;

        return Task.CompletedTask;
    }
}
