using System.IO.Abstractions;
using AwesomeAssertions;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Internal;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Tests.Tools.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Backend.Modules;
using Sdk.Testing;
using ViciOne.Cluster.Model;
using Xunit;

namespace ClusterManagement.Backend.Tests.Designs;

public class PackageStorageTests
{
    private readonly IClusterDependencyStore _dependencyStore = Substitute.For<IClusterDependencyStore>();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceService = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
    private readonly ILogger<PackageStore> _logger = Substitute.For<ILogger<PackageStore>>();

    private ServiceProvider CreateServices(IFileSystem? fileSystem = null)
    {
        fileSystem ??= new FileSystem();

        return new ServiceCollection()
            .AddSingleton<ClusterManagementOptions>()
            .AddSingleton<IPackageStore, PackageStore>()
            .AddSingleton(_dependencyStore)
            .AddSingleton(fileSystem)
            .AddSingleton(_workspaceService)
            .AddTransient(_ => _logger)
            .BuildServiceProvider();
    }

    public sealed class StorePackage : PackageStorageTests
    {
        [Fact]
        public async Task Should_extract_name_and_version_from_archive()
        {
            // Arrange
            using var services = CreateServices();
            using var tempDirectory = new TemporaryDirectory();
            var fileSystem = services.GetRequiredService<IFileSystem>();
            var destinationPath = Path.Combine(tempDirectory.Path, "ViciOne.Suite.ClusterManagement");
            var dependency = PackageFactory.ConstantPackageDependency;
            var sourceZipBytes = PackageFactory.CreatePackageArchiveFromDependency(dependency.Name);
            var store = services.GetRequiredService<IPackageStore>();

            _workspaceService.Cache.Returns(destinationPath);

            // Act
            await store.StorePackage(sourceZipBytes, true, CancellationToken.None);

            // Assert
            var expectedFbPath = fileSystem.Path.Combine(destinationPath, IFileSystemExtensions.DependenciesDirectory, dependency.Name);
            fileSystem.Directory.Exists(expectedFbPath).Should().BeTrue();
            fileSystem.Directory.Exists(fileSystem.Path.Combine(expectedFbPath, dependency.Version.ToString())).Should().BeTrue();
        }

        [Fact]
        public async Task Should_enqueue_package_install_operation()
        {
            // Arrange
            using var services = CreateServices();
            using var tempDirectory = new TemporaryDirectory();
            var destinationPath = Path.Combine(tempDirectory.Path, "ViciOne.Suite.ClusterManagement");
            var constantPackage = PackageFactory.ConstantPackageDependency;
            var sourceZipBytes = PackageFactory.CreatePackageArchiveFromDependency(constantPackage.Name);
            var store = services.GetRequiredService<IPackageStore>();

            _workspaceService.Cache.Returns(destinationPath);

            // Act
            var dependency = await store.StorePackage(sourceZipBytes, true, CancellationToken.None);

            // Assert
            dependency.Name.Should().Be(dependency.Name);
            dependency.Version.Should().Be(constantPackage.Version);

            await _dependencyStore.Received(1).EnqueueOperations(
                Arg.Is<List<ClusterDependencyOperation>>(d => d[0].Dependency.Name == constantPackage.Name
                && d[0].Dependency.Version == constantPackage.Version
                && d[0].OperationKind == ClusterDependencyOperationKind.Install),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_validate_header()
        {
            // Arrange
            using var services = CreateServices();
            using var tempDirectory = new TemporaryDirectory();
            var destinationPath = Path.Combine(tempDirectory.Path, "ViciOne.Suite.ClusterManagement");
            var constantPackage = PackageFactory.ConstantPackageDependency;

            var sourceZipBytes = PackageFactory.CreatePackageArchiveFromDependency(constantPackage.Name);
            var store = services.GetRequiredService<IPackageStore>();

            _workspaceService.Cache.Returns(destinationPath);

            // Act
            var dependency = await store.StorePackage(sourceZipBytes, true, CancellationToken.None);

            // Assert
            dependency.Name.Should().Be(constantPackage.Name);
            dependency.Version.Should().Be(constantPackage.Version);

            await _dependencyStore.Received(1).EnqueueOperations(
                Arg.Is<List<ClusterDependencyOperation>>(d => d[0].Dependency.Name == constantPackage.Name
                && d[0].Dependency.Version == constantPackage.Version
                && d[0].OperationKind == ClusterDependencyOperationKind.Install),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_if_archive_does_not_contain_deps_json_file()
        {
            // Arrange
            using var services = CreateServices();
            var store = services.GetRequiredService<IPackageStore>();
            var sourceZipBytes = PackageFactory.CreateInvalidPackageZipArchiveBytes("ViciOne.Test.Package");

            // Act
            var act = () => store.StorePackage(sourceZipBytes, true, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("The provided package archive does not contain a deps.json file.");
        }

        [Fact]
        public async Task Should_throw_if_we_can_get_aversion_from_deps_json_file()
        {
            // Arrange
            using var services = CreateServices();
            var store = services.GetRequiredService<IPackageStore>();

            var sourceZipBytes = PackageFactory.CreatePackageZipArchiveBytes("ViciOne.Test.Package", "0.46.4", string.Empty);

            // Act
            var act = () => store.StorePackage(sourceZipBytes, true, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Package version of 'ViciOne.Test.Package' not found in deps.json.");
        }
    }

    public sealed class DeletePackage : PackageStorageTests
    {
        [Fact]
        public async Task Should_delete_package_from_file_system_if_no_dependency_exists_for_it()
        {
            // Arrange            
            var fileSystem = Substitute.For<IFileSystem>();
            using var services = CreateServices(fileSystem);
            var store = services.GetRequiredService<IPackageStore>();
            var dependency = new ClusterDependency
            {
                Name = "ViciOne.Test.Package",
                Version = Version.Parse("0.46.4")
            };

            var packagePath = "some/path/ViciOne.Test.Package/0.46.4";

            _dependencyStore.LoadDependencies(Arg.Any<CancellationToken>())
                .Returns([]);

            fileSystem.Path.Combine(string.Empty, dependency.Name, dependency.Version.ToString())
                .Returns(packagePath);

            fileSystem.Path.Exists(packagePath).Returns(true);

            // Act
            await store.DeletePackage(dependency, CancellationToken.None);

            // Assert
            fileSystem.Directory.Received(0).Delete(path: Arg.Is<string>(packagePath), Arg.Any<bool>());
            await _dependencyStore.Received(1).EnqueueOperations(
                Arg.Is<List<ClusterDependencyOperation>>(d => d[0].Dependency.Name == dependency.Name
                && d[0].Dependency.Version == dependency.Version
                && d[0].OperationKind == ClusterDependencyOperationKind.Uninstall),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_not_delete_package_from_file_system_if_dependency_exists()
        {
            // Arrange            
            var fileSystem = Substitute.For<IFileSystem>();
            using var services = CreateServices(fileSystem);
            var store = services.GetRequiredService<IPackageStore>();
            var dependency = new ClusterDependency
            {
                Name = "ViciOne.Test.Package",
                Version = Version.Parse("0.46.4")
            };

            _dependencyStore.LoadDependencies(Arg.Any<CancellationToken>())
                .Returns([dependency]);

            // Act
            await store.DeletePackage(dependency, CancellationToken.None);

            // Assert
            fileSystem.Directory.Received(0).Delete(Arg.Any<string>(), Arg.Any<bool>());
            await _dependencyStore.Received(1).EnqueueOperations(
                Arg.Is<List<ClusterDependencyOperation>>(d => d[0].Dependency.Name == dependency.Name
                && d[0].Dependency.Version == dependency.Version
                && d[0].OperationKind == ClusterDependencyOperationKind.Uninstall),
                Arg.Any<CancellationToken>());
        }
    }
}
