using ClusterManagement.Client.Editor.ControlPanels.Packages.Services;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using ClusterManagement.Public.Events;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages.Extensions;

internal static class PackagesControlPanelStateExtensions
{
    extension(PackagesControlPanelState state)
    {
        public bool ApplyChanges(IEnumerable<ClusterDependencyChange> changes)
        {
            var hasChanges = false;

            foreach (var operation in changes)
            {
                var name = operation.Operation.Dependency.Name;
                var installed = state.InstalledQueryable.FirstOrDefault(p => p.Name == name);
                if (installed is not null)
                {
                    installed.PendingOperation = operation.Action is Sdk.Messaging.CrudAction.Deleted
                        ? null
                        : operation.Operation;
                    hasChanges = true;
                    continue;
                }

                var available = state.AvailableQueryable.FirstOrDefault(p => p.Name == name);
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
            var package = state.AvailableQueryable.FirstOrDefault(p => p.Name == packageName);
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
            state.AvailableQueryable = state.AvailableQueryable.Where(p => p.Name != packageName).AsQueryable();
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

                if (state.InstallOperations.Any(p => p.Dependency.Name == item.Name
                    && p.Dependency.Version.ToString() == item.Versions.First().Version))
                {
                    continue;
                }

                var dependency = new ClusterDependency
                {
                    Name = item.Name,
                    Version = item.Versions.First().Version,
                };

                state.InstallOperations.Add(new ClusterDependencyOperation(dependency, ClusterDependencyOperationKind.Install));
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
                    || state.InstallOperations.Any(p => p.Dependency.Name == item.Name
                    && p.Dependency.Version == item.AvailableUpdateVersion))
                {
                    continue;
                }

                var dependency = new ClusterDependency
                {
                    Name = item.Name,
                    Version = item.AvailableUpdateVersion,
                };

                state.InstallOperations.Add(new ClusterDependencyOperation(dependency, ClusterDependencyOperationKind.Install));
            }
        }

        public void UpdateUninstallOperations(IEnumerable<ClusterPackageInfo> packages)
        {
            foreach (var item in packages)
            {
                if (string.IsNullOrWhiteSpace(item.InstalledVersion))
                    continue;

                if (item.Error is not null &&
                    item.Error.ErrorCode == ClusterError.PackageFailedToLoad &&
                    !state.UninstallOperations.Any(p => p.Dependency.Name == item.Name && p.Dependency.Version.ToString() == item.InstalledVersion))
                {
                    UninstallItem(item);
                    continue;
                }

                if (item.Versions is null || item.Versions.Count == 0)
                {
                    continue;
                }

                if (state.UninstallOperations.Any(p => p.Dependency.Name == item.Name
                    && p.Dependency.Version.ToString() == item.Versions.First().Version))
                {
                    continue;
                }

                UninstallItem(item);
            }

            void UninstallItem(ClusterPackageInfo item)
            {
                var dependency = new ClusterDependency
                {
                    Name = item.Name,
                    Version = item.InstalledVersion ?? string.Empty,
                };

                state.UninstallOperations.Add(new ClusterDependencyOperation(dependency, ClusterDependencyOperationKind.Uninstall));
            }
        }

        internal void UpdateUninstallOperations(List<ClusterDependencyOperation> operations)
        {
            foreach (var operation in operations)
            {
                var exists = state.UninstallOperations.FirstOrDefault(k => k.Dependency.Name == operation.Dependency.Name && k.OperationKind == operation.OperationKind);
                if (exists is not null)
                    state.UninstallOperations.Remove(operation);

                state.UninstallOperations.Add(operation);
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

                    var exists = state.PackagesToBeDeleted.Any(p => p.Name == package.Name && p.Version == package.InstalledVersion);
                    if (exists)
                        continue;

                    state.PackagesToBeDeleted.Add(new ClusterPackageItem(package.Name, version.Version));
                }
            }
        }

        public void UpdatePackage(ClusterPackageInfo package)
        {
            if (!string.IsNullOrEmpty(package.InstalledVersion))
            {
                var updated = state.InstalledQueryable
                    .Where(p => p.Name != package.Name)
                    .Append(package)
                    .OrderBy(p => p.Name)
                    .AsQueryable();

                state.InstalledQueryable = updated;
            }
            else
            {
                var updated = state.AvailableQueryable
                    .Where(p => p.Name != package.Name)
                    .Append(package)
                    .OrderBy(p => p.Name)
                    .AsQueryable();

                state.AvailableQueryable = updated;
            }
        }
    }
}
