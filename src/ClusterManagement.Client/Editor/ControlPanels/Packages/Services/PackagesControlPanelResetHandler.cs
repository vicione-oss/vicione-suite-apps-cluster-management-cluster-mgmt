using ClusterManagement.Public.Services;
using Sdk.Client.ControlPanels.Services;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages.Services;

internal sealed class PackagesControlPanelResetHandler(
    IPackageService packageService,
    IResourceDownloadStateService resourceDownloadState) : IControlPanelResetHandler<PackagesControlPanelState>
{
    public async Task Reset(PackagesControlPanelState state, CancellationToken cancellationToken)
    {
        if (!state.HasPendingChanges() && (state.InstalledQueryable.Any() || state.AvailableQueryable.Any()))
            return;

        state.BeginLoading();

        try
        {
            await resourceDownloadState.WaitForCompletion(cancellationToken);

            state.InstallOperations.Clear();
            state.UninstallOperations.Clear();
            state.PackagesToBeDeleted.Clear();

            var allPackages = await packageService.GetPackages(null, cancellationToken);

            state.InstalledQueryable = allPackages.Where(k => k.InstalledVersion != null || k.RunningClusterVersion != null).AsQueryable();
            state.AvailableQueryable = allPackages.Where(k => string.IsNullOrEmpty(k.InstalledVersion) && string.IsNullOrEmpty(k.RunningClusterVersion)).AsQueryable();
        }
        finally
        {
            state.EndLoading();
        }
    }
}
