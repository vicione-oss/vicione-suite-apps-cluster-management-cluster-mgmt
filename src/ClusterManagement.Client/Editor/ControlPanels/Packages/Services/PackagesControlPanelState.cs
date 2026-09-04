using ClusterManagement.Internal.Contracts;
using Sdk.Client.ControlPanels.Services;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages.Services;

public sealed class PackagesControlPanelState : ControlPanelState
{
    internal IQueryable<ClusterPackageInfo> InstalledQueryable
    {
        get;
        set
        {
            if (value == field)
                return;

            field = value;

            OnPropertyChanged(nameof(InstalledQueryable));
        }
    } = Enumerable.Empty<ClusterPackageInfo>().AsQueryable();

    internal IQueryable<ClusterPackageInfo> AvailableQueryable
    {
        get;
        set
        {
            if (value == field)
                return;

            field = value;

            OnPropertyChanged(nameof(AvailableQueryable));
        }
    } = Enumerable.Empty<ClusterPackageInfo>().AsQueryable();

    internal string? InstalledFilterText { get; set; }
    internal string? AvailableFilterText { get; set; }

    internal List<ClusterDependencyOperation> InstallOperations { get; } = [];

    internal List<ClusterDependencyOperation> UninstallOperations { get; } = [];

    internal List<ClusterPackageItem> PackagesToBeDeleted { get; } = [];

    internal bool HasPendingChanges()
        => InstallOperations.Count > 0 || UninstallOperations.Count > 0 || PackagesToBeDeleted.Count > 0;
}

internal record ClusterPackageItem(string Name, string Version);
