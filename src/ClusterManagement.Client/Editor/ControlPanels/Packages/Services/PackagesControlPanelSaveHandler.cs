using ClusterManagement.Client.Editor.ControlPanels.Packages.Models;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages.Services;

internal sealed class PackagesControlPanelSaveHandler(IPackageService packageService)
        : IControlPanelSaveHandler<PackagesControlPanelState>
{
    public async Task<ISaveResult> Save(PackagesControlPanelState state, CancellationToken cancellationToken)
    {
        if (!state.HasPendingChanges())
            return new SaveSuccessResult();

        // pending changes
        var operations = state.InstallOperations.Concat(state.UninstallOperations).ToList();
        if (operations.Count > 0)
        {
            var result = await packageService.UpdatePackages(operations, cancellationToken);
            if (result is PackageServiceErrorResult error)
                return new SaveErrorResult(error.ErrorMessage, error.ErrorCode);

            state.InstallOperations.Clear();
            state.UninstallOperations.Clear();
        }

        // deletions
        var toBeDeleted = state.PackagesToBeDeleted.ToArray();
        foreach (var package in toBeDeleted)
        {
            var deleteResult = await packageService.DeletePackage(package.Name, package.Version, cancellationToken);
            if (deleteResult is PackageServiceErrorResult deleteError)
                return new SaveErrorResult(deleteError.ErrorMessage, deleteError.ErrorCode);

            state.PackagesToBeDeleted.Remove(package);
        }

        return new SaveSuccessResult();
    }
}
