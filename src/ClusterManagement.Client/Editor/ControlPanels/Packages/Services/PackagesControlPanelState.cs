using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages.Services;

public sealed class PackagesControlPanelState : ControlPanelState
{
    internal IQueryable<ClusterPackageInfo> InstalledQueryable
    {
        get => field;
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
        get => field;
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

    public bool ApplyChanges(IEnumerable<ClusterDependencyChange> changes)
    {
        var hasChanges = false;

        foreach (var operation in changes)
        {
            var name = operation.Operation.Dependency.Name;
            var installed = InstalledQueryable.FirstOrDefault(p => p.Name == name);
            if (installed is not null)
            {
                installed.PendingOperation = operation.Action is Sdk.Messaging.CrudAction.Deleted
                    ? null
                    : operation.Operation;
                hasChanges = true;
                continue;
            }

            var available = AvailableQueryable.FirstOrDefault(p => p.Name == name);
            if (available is not null)
            {
                available.PendingOperation = operation.Action is Sdk.Messaging.CrudAction.Deleted
                    ? null
                    : operation.Operation;
                hasChanges = true;
            }
        }

        return hasChanges;
    }

    public bool DeletePackage(string packageName, string version)
    {
        var package = AvailableQueryable.FirstOrDefault(p => p.Name == packageName);
        if (package is null)
            return false;

        // the package version might be deleted but there mmight be other versions
        // only if was a package that was only installed locally we can remove here
        var deletedVersion = package.Versions?.FirstOrDefault(v => v.Version == version);
        if (deletedVersion is null)
            return false;

        // we have to update the readonly collection to remove the version
        package.Versions = [.. package.Versions!.Where(v => v.Version != deletedVersion.Version)];
        if (package.Versions.Count > 0)
            return false;

        // now we have no versions left and can remove the package entry
        AvailableQueryable = AvailableQueryable.Where(p => p.Name != packageName).AsQueryable();
        return true;
    }

    public void UpdateInstallOperations(IEnumerable<ClusterPackageInfo> packages)
    {
        foreach (var item in packages)
        {
            if (item.Versions is null || item.Versions.Count == 0)
            {
                continue;
            }

            if (InstallOperations.Any(p => p.Dependency.Name == item.Name
                && p.Dependency.Version.ToString() == item.Versions.First().Version))
            {
                continue;
            }

            var dependency = new ClusterDependency
            {
                Name = item.Name,
                Version = item.Versions.First().Version,
            };

            InstallOperations.Add(new ClusterDependencyOperation(dependency, ClusterDependencyOperationKind.Install));
        }
    }

    public void UpdateUpdateOperations(IEnumerable<ClusterPackageInfo> packages)
    {
        foreach (var item in packages)
        {
            if (item.Versions is null || item.Versions.Count == 0)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.InstalledVersion)
                || string.IsNullOrEmpty(item.AvailableUpdateVersion)
                || InstallOperations.Any(p => p.Dependency.Name == item.Name
                && p.Dependency.Version == item.AvailableUpdateVersion))
            {
                continue;
            }

            var dependecy = new ClusterDependency
            {
                Name = item.Name,
                Version = item.AvailableUpdateVersion,
            };

            InstallOperations.Add(new ClusterDependencyOperation(dependecy, ClusterDependencyOperationKind.Install));
        }
    }

    public void UpdateUninstallOperations(IEnumerable<ClusterPackageInfo> packages)
    {
        foreach (var item in packages)
        {
            if (item.Versions is null || item.Versions.Count == 0)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.InstalledVersion)
                || UninstallOperations.Any(p => p.Dependency.Name == item.Name
                && p.Dependency.Version.ToString() == item.Versions.First().Version))
            {
                continue;
            }

            var dependecy = new ClusterDependency
            {
                Name = item.Name,
                Version = item.InstalledVersion,
            };

            UninstallOperations.Add(new ClusterDependencyOperation(dependecy, ClusterDependencyOperationKind.Uninstall));
        }
    }

    internal void UpdateUninstallOperations(List<ClusterDependencyOperation> operations)
    {
        foreach (var operation in operations)
        {
            var exists = UninstallOperations.FirstOrDefault(k => k.Dependency.Name == operation.Dependency.Name && k.OperationKind == operation.OperationKind);
            if (exists is not null)
                UninstallOperations.Remove(operation);

            UninstallOperations.Add(operation);
        }
    }

    public void UpdatePackagesToBeDeleted(IEnumerable<ClusterPackageInfo> packages)
    {
        foreach (var package in packages)
        {
            if (package.Versions is null)
                continue;

            foreach (var version in package.Versions)
            {
                // we don't have it locally so nothing to delete
                if (!version.IsLocal)
                    continue;

                var exists = PackagesToBeDeleted.Any(p => p.Name == package.Name && p.Version == package.InstalledVersion);
                if (exists)
                    continue;

                PackagesToBeDeleted.Add(new ClusterPackageItem(package.Name, version.Version));
            }
        }
    }

    public void UpdatePackage(ClusterPackageInfo package)
    {
        if (!string.IsNullOrEmpty(package.InstalledVersion))
        {
            var updated = InstalledQueryable
                .Where(p => p.Name != package.Name)
                .Append(package)
                .OrderBy(p => p.Name)
                .AsQueryable();

            InstalledQueryable = updated;
        }
        else
        {
            var updated = AvailableQueryable
                .Where(p => p.Name != package.Name)
                .Append(package)
                .OrderBy(p => p.Name)
                .AsQueryable();

            AvailableQueryable = updated;
        }
    }

    internal bool HasPendingChanges()
        => InstallOperations.Count > 0 || UninstallOperations.Count > 0 || PackagesToBeDeleted.Count > 0;
}

internal record ClusterPackageItem(string Name, string Version);
