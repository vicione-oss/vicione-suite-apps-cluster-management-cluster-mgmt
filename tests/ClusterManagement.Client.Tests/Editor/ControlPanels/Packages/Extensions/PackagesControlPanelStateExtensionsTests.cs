using ClusterManagement.Client.Editor.ControlPanels.Packages.Services;
using ClusterManagement.Client.Editor.ControlPanels.Packages.Extensions;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using Sdk.Messaging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Tests.Editor.ControlPanels.Packages.Extensions;

public class PackagesControlPanelStateExtensionsTests
{
    private readonly ClusterDependency _dependency = new()
    {
        Name = "ViciOne.TestPackage",
        Version = "1.0.0"
    };

    private static ClusterPackageInfo CreatePackageInfo(
        string name,
        string? installedVersion = null,
        params string[] versions) => new()
        {
            Name = name,
            InstalledVersion = installedVersion,
            Versions = versions.Length > 0
            ? versions.Select(v => new ClusterPackageVersion(v)).ToList()
            : null
        };

    public sealed class ApplyChanges : PackagesControlPanelStateExtensionsTests
    {
        [Fact]
        public void Should_set_pending_operation_on_installed_package()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var package = CreatePackageInfo(_dependency.Name, installedVersion: "1.0.0");
            state.InstalledQueryable = new[] { package }.AsQueryable();
            var operation = new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Install);

            // Act
            var result = state.ApplyChanges([new ClusterDependencyChange(CrudAction.Created, operation)]);

            // Assert
            result.Should().BeTrue();
            package.PendingOperation.Should().Be(operation);
        }

        [Fact]
        public void Should_clear_pending_operation_on_installed_package_when_action_is_deleted()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var operation = new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Install);
            var package = CreatePackageInfo(_dependency.Name, installedVersion: "1.0.0");
            package.PendingOperation = operation;
            state.InstalledQueryable = new[] { package }.AsQueryable();

            // Act
            var result = state.ApplyChanges([new ClusterDependencyChange(CrudAction.Deleted, operation)]);

            // Assert
            result.Should().BeTrue();
            package.PendingOperation.Should().BeNull();
        }

        [Fact]
        public void Should_set_pending_operation_on_available_package_when_not_in_installed()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var package = CreatePackageInfo(_dependency.Name);
            state.AvailableQueryable = new[] { package }.AsQueryable();
            var operation = new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Install);

            // Act
            var result = state.ApplyChanges([new ClusterDependencyChange(CrudAction.Created, operation)]);

            // Assert
            result.Should().BeTrue();
            package.PendingOperation.Should().Be(operation);
        }

        [Fact]
        public void Should_return_false_when_no_matching_package_found()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var operation = new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Install);

            // Act
            var result = state.ApplyChanges([new ClusterDependencyChange(CrudAction.Created, operation)]);

            // Assert
            result.Should().BeFalse();
        }
    }

    public sealed class DeletePackage : PackagesControlPanelStateExtensionsTests
    {
        [Fact]
        public void Should_return_false_when_package_not_in_available()
        {
            // Arrange
            var state = new PackagesControlPanelState();

            // Act
            var result = state.DeletePackage("Unknown.Package", "1.0.0");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void Should_return_false_when_version_not_found()
        {
            // Arrange
            var state = new PackagesControlPanelState
            {
                AvailableQueryable = new[] { CreatePackageInfo(_dependency.Name, versions: "1.0.0") }.AsQueryable()
            };

            // Act
            var result = state.DeletePackage(_dependency.Name, "9.9.9");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void Should_remove_package_from_available_when_last_version_is_deleted()
        {
            // Arrange
            var state = new PackagesControlPanelState
            {
                AvailableQueryable = new[] { CreatePackageInfo(_dependency.Name, versions: "1.0.0") }.AsQueryable()
            };

            // Act
            var result = state.DeletePackage(_dependency.Name, "1.0.0");

            // Assert
            result.Should().BeTrue();
            state.AvailableQueryable.Should().NotContain(p => p.Name == _dependency.Name);
        }

        [Fact]
        public void Should_return_false_and_remove_version_when_other_versions_remain()
        {
            // Arrange
            var state = new PackagesControlPanelState
            {
                AvailableQueryable = new[] { CreatePackageInfo(_dependency.Name, versions: ["1.0.0", "2.0.0"]) }.AsQueryable()
            };

            // Act
            var result = state.DeletePackage(_dependency.Name, "1.0.0");

            // Assert
            result.Should().BeFalse();
            state.AvailableQueryable.Single(p => p.Name == _dependency.Name)
                .Versions.Should().ContainSingle(v => v.Version == "2.0.0");
        }
    }

    public sealed class UpdateInstallOperations : PackagesControlPanelStateExtensionsTests
    {
        [Fact]
        public void Should_add_install_operation_for_each_package()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var packages = new[] { CreatePackageInfo(_dependency.Name, versions: "1.0.0") };

            // Act
            state.UpdateInstallOperations(packages);

            // Assert
            state.InstallOperations.Should().ContainSingle(op =>
                op.Dependency.Name == _dependency.Name &&
                op.OperationKind == ClusterDependencyOperationKind.Install);
        }

        [Fact]
        public void Should_skip_packages_with_no_versions()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var packages = new[] { CreatePackageInfo(_dependency.Name) };

            // Act
            state.UpdateInstallOperations(packages);

            // Assert
            state.InstallOperations.Should().BeEmpty();
        }

        [Fact]
        public void Should_not_add_duplicate_operations()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var packages = new[] { CreatePackageInfo(_dependency.Name, versions: "1.0.0") };
            state.UpdateInstallOperations(packages);

            // Act
            state.UpdateInstallOperations(packages);

            // Assert
            state.InstallOperations.Should().HaveCount(1);
        }
    }

    public sealed class UpdateUpdateOperations : PackagesControlPanelStateExtensionsTests
    {
        [Fact]
        public void Should_add_install_operation_for_each_package()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var package = CreatePackageInfo(_dependency.Name, installedVersion: "1.0.0", versions: ["1.0.0", "2.0.0"]);
            package.AvailableUpdateVersion = "2.0.0";
            var packages = new[] { package };

            // Act
            state.UpdateUpdateOperations(packages);

            // Assert
            state.InstallOperations.Should().ContainSingle(op =>
                op.Dependency.Name == _dependency.Name &&
                op.Dependency.Version == package.AvailableUpdateVersion &&
                op.OperationKind == ClusterDependencyOperationKind.Install);
        }

        [Fact]
        public void Should_skip_packages_with_no_versions()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var packages = new[] { CreatePackageInfo(_dependency.Name) };

            // Act
            state.UpdateUpdateOperations(packages);

            // Assert
            state.InstallOperations.Should().BeEmpty();
        }

        [Fact]
        public void Should_not_add_duplicate_operations()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var package = CreatePackageInfo(_dependency.Name, installedVersion: "1.0.0", versions: ["1.0.0", "2.0.0"]);
            package.AvailableUpdateVersion = "2.0.0";
            var packages = new[] { package };
            state.UpdateUpdateOperations(packages);

            // Act
            state.UpdateUpdateOperations(packages);

            // Assert
            state.InstallOperations.Should().HaveCount(1);
        }
    }

    public sealed class UpdateUninstallOperations : PackagesControlPanelStateExtensionsTests
    {
        [Fact]
        public void Should_add_uninstall_operation_for_installed_packages()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var packages = new[] { CreatePackageInfo(_dependency.Name, installedVersion: "1.0.0", versions: "1.0.0") };

            // Act
            state.UpdateUninstallOperations(packages);

            // Assert
            state.UninstallOperations.Should().ContainSingle(op =>
                op.Dependency.Name == _dependency.Name &&
                op.OperationKind == ClusterDependencyOperationKind.Uninstall);
        }

        [Fact]
        public void Should_skip_packages_without_installed_version()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var packages = new[] { CreatePackageInfo(_dependency.Name, versions: "1.0.0") };

            // Act
            state.UpdateUninstallOperations(packages);

            // Assert
            state.UninstallOperations.Should().BeEmpty();
        }

        [Fact]
        public void Should_skip_packages_with_no_versions()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var packages = new[] { CreatePackageInfo(_dependency.Name, installedVersion: "1.0.0") };

            // Act
            state.UpdateUninstallOperations(packages);

            // Assert
            state.UninstallOperations.Should().BeEmpty();
        }

        [Fact]
        public void Should_not_add_duplicate_operations()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var packages = new[] { CreatePackageInfo(_dependency.Name, installedVersion: "1.0.0", versions: "1.0.0") };
            state.UpdateUninstallOperations(packages);

            // Act
            state.UpdateUninstallOperations(packages);

            // Assert
            state.UninstallOperations.Should().HaveCount(1);
        }
    }

    public sealed class UpdatePackagesToBeDeleted : PackagesControlPanelStateExtensionsTests
    {
        [Fact]
        public void Should_add_local_package_versions()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var package = new ClusterPackageInfo
            {
                Name = _dependency.Name,
                InstalledVersion = "1.0.0",
                Versions = [new ClusterPackageVersion("1.0.0") { IsLocal = true }]
            };

            // Act
            state.UpdatePackagesToBeDeleted([package]);

            // Assert
            state.PackagesToBeDeleted.Should().ContainSingle(p =>
                p.Name == _dependency.Name && p.Version == "1.0.0");
        }

        [Fact]
        public void Should_skip_non_local_versions()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var package = new ClusterPackageInfo
            {
                Name = _dependency.Name,
                InstalledVersion = "1.0.0",
                Versions = [new ClusterPackageVersion("1.0.0") { IsLocal = false }]
            };

            // Act
            state.UpdatePackagesToBeDeleted([package]);

            // Assert
            state.PackagesToBeDeleted.Should().BeEmpty();
        }

        [Fact]
        public void Should_not_add_already_pending_packages()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var package = new ClusterPackageInfo
            {
                Name = _dependency.Name,
                InstalledVersion = "1.0.0",
                Versions = [new ClusterPackageVersion("1.0.0") { IsLocal = true }]
            };
            state.UpdatePackagesToBeDeleted([package]);

            // Act
            state.UpdatePackagesToBeDeleted([package]);

            // Assert
            state.PackagesToBeDeleted.Should().HaveCount(1);
        }
    }

    public sealed class UpdatePackage : PackagesControlPanelStateExtensionsTests
    {
        [Fact]
        public void Should_add_to_installed_when_package_has_installed_version()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var package = CreatePackageInfo(_dependency.Name, installedVersion: "1.0.0");

            // Act
            state.UpdatePackage(package);

            // Assert
            state.InstalledQueryable.Should().Contain(package);
        }

        [Fact]
        public void Should_replace_existing_entry_in_installed()
        {
            // Arrange
            var state = new PackagesControlPanelState
            {
                InstalledQueryable = new[] { CreatePackageInfo(_dependency.Name, installedVersion: "1.0.0") }.AsQueryable()
            };
            var updated = CreatePackageInfo(_dependency.Name, installedVersion: "2.0.0");

            // Act
            state.UpdatePackage(updated);

            // Assert
            state.InstalledQueryable.Should().ContainSingle()
                .Which.InstalledVersion.Should().Be("2.0.0");
        }

        [Fact]
        public void Should_add_to_available_when_package_has_no_installed_version()
        {
            // Arrange
            var state = new PackagesControlPanelState();
            var package = CreatePackageInfo(_dependency.Name);

            // Act
            state.UpdatePackage(package);

            // Assert
            state.AvailableQueryable.Should().Contain(package);
        }

        [Fact]
        public void Should_replace_existing_entry_in_available()
        {
            // Arrange
            var state = new PackagesControlPanelState
            {
                AvailableQueryable = new[] { CreatePackageInfo(_dependency.Name) }.AsQueryable()
            };
            var updated = CreatePackageInfo(_dependency.Name);

            // Act
            state.UpdatePackage(updated);

            // Assert
            state.AvailableQueryable.Should().ContainSingle()
                .Which.Should().Be(updated);
        }
    }

    public sealed class HasPendingChanges : PackagesControlPanelStateExtensionsTests
    {
        [Fact]
        public void Should_return_false_when_all_lists_are_empty()
        {
            var state = new PackagesControlPanelState();

            state.HasPendingChanges().Should().BeFalse();
        }

        [Fact]
        public void Should_return_true_when_install_operations_are_present()
        {
            var state = new PackagesControlPanelState();
            state.InstallOperations.Add(new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Install));

            state.HasPendingChanges().Should().BeTrue();
        }

        [Fact]
        public void Should_return_true_when_uninstall_operations_are_present()
        {
            var state = new PackagesControlPanelState();
            state.UninstallOperations.Add(new ClusterDependencyOperation(_dependency, ClusterDependencyOperationKind.Uninstall));

            state.HasPendingChanges().Should().BeTrue();
        }

        [Fact]
        public void Should_return_true_when_packages_to_be_deleted_are_present()
        {
            var state = new PackagesControlPanelState();
            state.PackagesToBeDeleted.Add(new ClusterPackageItem(_dependency.Name, "1.0.0"));

            state.HasPendingChanges().Should().BeTrue();
        }
    }
}
