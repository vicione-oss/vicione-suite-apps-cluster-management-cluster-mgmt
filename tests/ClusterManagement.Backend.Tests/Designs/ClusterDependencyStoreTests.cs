using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using AwesomeAssertions;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Internal;
using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sdk.Backend.Modules;
using Sdk.Messaging;
using ViciOne.Cluster.Model;
using Xunit;

namespace ClusterManagement.Backend.Tests.Designs;

public class ClusterDependencyStoreTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
    private readonly IOptions<ClusterManagementOptions> _options;
    private readonly ILogger<ClusterDependencyStore> _logger = Substitute.For<ILogger<ClusterDependencyStore>>();
    private readonly IServiceProvider _services;

    private const string WorkspacePath = "C:\\Workspace";
    private const string HomePath = "C:\\Home";
    private const string QueueFileName = "packages-ops.json";

    public ClusterDependencyStoreTests()
    {
        _fileSystem = new MockFileSystem();
        _fileSystem.AddDirectory(WorkspacePath);
        _fileSystem.AddDirectory(HomePath);

        _workspaceProvider.Cache.Returns(WorkspacePath);
        _workspaceProvider.Home.Returns(HomePath);

        _options = Options.Create(new ClusterManagementOptions
        {
            PackageSet = null
        });

        _services = new ServiceCollection()
            .AddSingleton<IFileSystem>(_fileSystem)
            .AddSingleton(_workspaceProvider)
            .AddSingleton(_options)
            .AddSingleton(_logger)
            .AddSingleton<IClusterDependencyStore, ClusterDependencyStore>()
            .BuildServiceProvider();
    }

    public class BuildStoredPackageVersionMap : ClusterDependencyStoreTests
    {
        [Fact]
        public void Should_build_package_version_map_for_stored_packages()
        {
            // Arrange            
            var store = _services.GetRequiredService<IClusterDependencyStore>();

            var package1 = new ClusterDependency { Name = "Test.Package1", Version = new Version(1, 0, 0) };
            var package2 = new ClusterDependency { Name = "Test.Package2", Version = new Version(2, 2, 0) };
            var package3 = new ClusterDependency { Name = "Test.Package3", Version = new Version(1, 0, 11) };

            SetupStoredPackageVersions([package1, package2, package3]);

            // Act
            var result = store.BuildStoredPackageVersionMap();

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain(d => d.Key == package1.Name && d.Value.Any(v => v == package1.Version));
            result.Should().Contain(d => d.Key == package2.Name && d.Value.Any(v => v == package2.Version));
            result.Should().Contain(d => d.Key == package3.Name && d.Value.Any(v => v == package3.Version));
        }

        [Fact]
        public void Should_allow_multiple_package_versions()
        {
            // Arrange            
            var store = _services.GetRequiredService<IClusterDependencyStore>();

            var package1 = new ClusterDependency { Name = "Test.Package1", Version = new Version(1, 0, 0) };
            var package2 = new ClusterDependency { Name = "Test.Package1", Version = new Version(2, 2, 0) };
            var package3 = new ClusterDependency { Name = "Test.Package1", Version = new Version(1, 0, 11) };

            SetupStoredPackageVersions([package1, package2, package3]);

            // Act
            var result = store.BuildStoredPackageVersionMap();

            // Assert
            result.Should().HaveCount(1);
            result.Should().Contain(d => d.Key == package1.Name && d.Value.Count() == 3);
        }
    }

    public sealed class LoadDependencies : ClusterDependencyStoreTests
    {
        [Fact]
        public async Task Should_load_dependencies_from_packages_file()
        {
            // Arrange            
            var store = _services.GetRequiredService<IClusterDependencyStore>();

            SetupPackagesFile([
                new() { Name = "TestPackage1", Version = new Version(1, 0, 0) },
                new() { Name = "TestPackage2", Version = new Version(2, 0, 0) }
            ]);

            // Act
            var result = await store.LoadDependencies(CancellationToken.None);

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(d => d.Name == "TestPackage1" && d.Version == new Version(1, 0, 0));
            result.Should().Contain(d => d.Name == "TestPackage2" && d.Version == new Version(2, 0, 0));
        }

        [Fact]
        public async Task Should_load_dependencies_from_variant_packages_file()
        {
            // Arrange
            var options = Options.Create(new ClusterManagementOptions
            {
                PackageSet = "edge-s"
            });

            var serviceProvider = new ServiceCollection()
                .AddSingleton<IFileSystem>(_fileSystem)
                .AddSingleton(_workspaceProvider)
                .AddSingleton(options)
                .AddSingleton(_logger)
                .AddSingleton<IClusterDependencyStore, ClusterDependencyStore>()
                .BuildServiceProvider();

            var dependencies = new List<ClusterDependency>
            {
                new() { Name = "EdgePackage", Version = new Version(1, 0, 0) }
            };

            var packagesFile = _fileSystem.Path.Combine(HomePath, $"{ClusterDependencyStore.PackagesFileName}.edge-s.{ClusterDependencyStore.PackagesFileExtension}");
            var json = JsonSerializer.Serialize(dependencies, DefaultJsonSerializerSettings.Default);
            _fileSystem.AddFile(packagesFile, new MockFileData(json));

            var store = serviceProvider.GetRequiredService<IClusterDependencyStore>();

            // Act
            var result = await store.LoadDependencies(CancellationToken.None);

            // Assert
            result.Should().HaveCount(1);
            result.Should().Contain(d => d.Name == "EdgePackage");
        }
    }

    public sealed class QueueDependencyOperations : ClusterDependencyStoreTests
    {
        [Fact]
        public async Task Should_create_update_file_with_queued_dependencies()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            var dependency = new ClusterDependency() { Name = "TestPackage", Version = new Version(1, 0, 0) };

            SetupPackagesFile([]);

            var operations = new List<ClusterDependencyOperation>
            {
                new(dependency, ClusterDependencyOperationKind.Install)
            };

            // Act
            await store.EnqueueOperations(operations, CancellationToken.None);

            // Assert
            var updateFile = _fileSystem.Path.Combine(HomePath, QueueFileName);
            _fileSystem.File.Exists(updateFile).Should().BeTrue();

            var queued = await DeserializeClusterDependencyOperations(updateFile);

            queued.Should().HaveCount(1);
            queued.Should().Contain(d => d.Dependency == dependency && d.Operation == ClusterDependencyOperationKind.Install);
        }

        [Fact]
        public async Task Should_merge_with_existing_queued_operations()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            var queuedDependency = new ClusterDependency() { Name = "QueuedPackage", Version = new Version(1, 0, 0) };
            var newDependency = new ClusterDependency() { Name = "NewPackage", Version = new Version(2, 0, 0) };
            var otherDependency = new ClusterDependency() { Name = "OtherPackage", Version = new Version(1, 4, 2) };

            SetupPackagesFile([]);

            var updateFile = SetupUpdateQueueFile([
                new ClusterDependencyOperation(queuedDependency, ClusterDependencyOperationKind.Install)
            ]);

            var operations = new List<ClusterDependencyOperation>
            {
                new(newDependency, ClusterDependencyOperationKind.Install),
                new(otherDependency, ClusterDependencyOperationKind.Install)
            };

            // Act
            await store.EnqueueOperations(operations, CancellationToken.None);

            // Assert
            var queued = await DeserializeClusterDependencyOperations(updateFile);

            queued.Should().HaveCount(3);
            queued.Should().Contain(d => d.Dependency == queuedDependency && d.Operation == ClusterDependencyOperationKind.Install);
            queued.Should().Contain(d => d.Dependency == newDependency && d.Operation == ClusterDependencyOperationKind.Install);
            queued.Should().Contain(d => d.Dependency == otherDependency && d.Operation == ClusterDependencyOperationKind.Install);
        }

        [Fact]
        public async Task Should_overwrite_existing_package_with_same_name()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            var existingDependency = new ClusterDependency() { Name = "TestPackage", Version = new Version(1, 0, 0) };
            var overwriteDependency = new ClusterDependency() { Name = "TestPackage", Version = new Version(2, 0, 0) };

            SetupPackagesFile([]);

            var updateFile = SetupUpdateQueueFile([
                new ClusterDependencyOperation(existingDependency, ClusterDependencyOperationKind.Install)
            ]);

            var operations = new List<ClusterDependencyOperation>
            {
                new(overwriteDependency, ClusterDependencyOperationKind.Install)
            };

            // Act
            await store.EnqueueOperations(operations, CancellationToken.None);

            // Assert
            var queued = await DeserializeClusterDependencyOperations(updateFile);

            queued.Should().HaveCount(1);
            queued.Should().Contain(d => d.Dependency == overwriteDependency && d.Operation == ClusterDependencyOperationKind.Install);
        }

        [Fact]
        public async Task Should_not_create_file_when_no_dependencies_to_queue()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            var operations = new List<ClusterDependencyOperation>();

            // Act
            await store.EnqueueOperations(operations, CancellationToken.None);

            // Assert
            var updateFile = _fileSystem.Path.Combine(HomePath, QueueFileName);
            _fileSystem.File.Exists(updateFile).Should().BeFalse();
        }

        [Fact]
        public async Task Should_remove_redundant_install_package_operations()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            var installedDependency = new ClusterDependency { Name = "TestPackage", Version = new Version(1, 0, 0) };

            SetupPackagesFile([installedDependency]);

            var operations = new List<ClusterDependencyOperation>
            {
                new(installedDependency, ClusterDependencyOperationKind.Install)
            };

            // Act
            await store.EnqueueOperations(operations, CancellationToken.None);

            // Assert
            var updateFile = _fileSystem.Path.Combine(HomePath, QueueFileName);
            _fileSystem.File.Exists(updateFile).Should().BeFalse();
        }

        [Fact]
        public async Task Should_remove_redundant_uninstall_package_operations()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            var installedDependency = new ClusterDependency { Name = "TestPackage", Version = new Version(1, 0, 0) };
            var deleteDependency = new ClusterDependency() { Name = "TestPackage", Version = new Version(2, 0, 0) };

            SetupPackagesFile([installedDependency]);

            var operations = new List<ClusterDependencyOperation>
            {
                new(deleteDependency, ClusterDependencyOperationKind.Uninstall)
            };

            // Act
            await store.EnqueueOperations(operations, CancellationToken.None);

            // Assert
            var updateFile = _fileSystem.Path.Combine(HomePath, QueueFileName);
            _fileSystem.File.Exists(updateFile).Should().BeFalse();
        }
    }

    public sealed class ProcessQueuedOperations : ClusterDependencyStoreTests
    {
        [Fact]
        public async Task Should_merge_queued_operations_into_packages_file()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            var existingDependency = new ClusterDependency() { Name = "ExistingPackage", Version = new Version(1, 0, 0) };
            var newDependency = new ClusterDependency() { Name = "NewPackage", Version = new Version(2, 0, 0) };

            var packagesFile = SetupPackagesFile([existingDependency]);

            SetupUpdateQueueFile([new ClusterDependencyOperation(newDependency, ClusterDependencyOperationKind.Install)]);

            // Act
            await store.ProcessQueuedOperations(CancellationToken.None);

            // Assert
            var packages = await DeserializeClusterDependencies(packagesFile);

            packages.Should().HaveCount(2);
            packages.Should().Contain(d => d == existingDependency);
            packages.Should().Contain(d => d == newDependency);
        }

        [Fact]
        public async Task Should_overwrite_existing_package_with_queued_version()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            var existingDependency = new ClusterDependency() { Name = "TestPackage", Version = new Version(1, 0, 0) };
            var updateDependency = new ClusterDependency() { Name = "TestPackage", Version = new Version(2, 0, 0) };

            var packagesFile = SetupPackagesFile([existingDependency]);

            SetupUpdateQueueFile([new ClusterDependencyOperation(updateDependency, ClusterDependencyOperationKind.Install)]);

            // Act
            await store.ProcessQueuedOperations(CancellationToken.None);

            // Assert
            var packages = await DeserializeClusterDependencies(packagesFile);

            packages.Should().HaveCount(1);
            packages.Should().Contain(d => d == updateDependency);
        }

        [Fact]
        public async Task Should_do_nothing_when_no_queued_operations_exist()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            var existingDependency = new ClusterDependency() { Name = "TestPackage", Version = new Version(1, 0, 0) };
            var packagesFile = SetupPackagesFile([existingDependency]);

            // Act
            await store.ProcessQueuedOperations(CancellationToken.None);

            // Assert
            var packages = await DeserializeClusterDependencies(packagesFile);

            packages.Should().HaveCount(1);
            packages.Should().Contain(d => d == existingDependency);
        }

        [Fact]
        public async Task Should_remove_dependencies_on_operation_kind_uninstall()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            var existingDependency = new ClusterDependency() { Name = "TestPackage", Version = new Version(1, 0, 0) };
            var toBeRemovedDependency = new ClusterDependency() { Name = "ToBeRemovedPackage", Version = new Version(1, 0, 0) };

            var packagesFile = SetupPackagesFile([existingDependency, toBeRemovedDependency]);

            SetupUpdateQueueFile([new ClusterDependencyOperation(toBeRemovedDependency, ClusterDependencyOperationKind.Uninstall)]);

            // Act
            await store.ProcessQueuedOperations(CancellationToken.None);

            // Assert
            var packages = await DeserializeClusterDependencies(packagesFile);

            packages.Should().HaveCount(1);
            packages.Should().Contain(d => d == existingDependency);
        }

        [Fact]
        public async Task Should_remove_queued_update_file_after_successful_update()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            var existingDependency = new ClusterDependency() { Name = "TestPackage", Version = new Version(1, 0, 0) };
            var updateDependency = new ClusterDependency() { Name = "TestPackage", Version = new Version(2, 0, 0) };

            SetupPackagesFile([existingDependency]);

            SetupUpdateQueueFile([new ClusterDependencyOperation(updateDependency, ClusterDependencyOperationKind.Install)]);

            // Act
            await store.ProcessQueuedOperations(CancellationToken.None);

            // Assert
            var updateFile = _fileSystem.Path.Combine(HomePath, QueueFileName);
            _fileSystem.File.Exists(updateFile).Should().BeFalse();
        }
    }

    public class GetPackagesFilepath : ClusterDependencyStoreTests
    {
        [Theory]
        [InlineData(null, $"{ClusterDependencyStore.PackagesFileName}.{ClusterDependencyStore.PackagesFileExtension}")]
        [InlineData("", $"{ClusterDependencyStore.PackagesFileName}.{ClusterDependencyStore.PackagesFileExtension}")]
        [InlineData("variant", $"{ClusterDependencyStore.PackagesFileName}.variant.{ClusterDependencyStore.PackagesFileExtension}")]
        [InlineData("edge-s", $"{ClusterDependencyStore.PackagesFileName}.edge-s.{ClusterDependencyStore.PackagesFileExtension}")]
        [InlineData("EDGE-m", $"{ClusterDependencyStore.PackagesFileName}.edge-m.{ClusterDependencyStore.PackagesFileExtension}")]
        public void Composes_filename_correctly(string? variant, string expected)
        {
            var packagesFilename = ClusterDependencyStore.GetPackagesFilepath("basePath", variant, new MockFileSystem());
            Path.GetFileName(packagesFilename).Should().Be(expected);
        }
    }

    private string SetupPackagesFile(List<ClusterDependency> dependencies)
    {
        var packagesFile = _fileSystem.Path.Combine(HomePath, $"{ClusterDependencyStore.PackagesFileName}.{ClusterDependencyStore.PackagesFileExtension}");
        var packagesJson = JsonSerializer.Serialize(dependencies, DefaultJsonSerializerSettings.Default);
        _fileSystem.AddFile(packagesFile, new MockFileData(packagesJson));

        return packagesFile;
    }

    private void SetupStoredPackageVersions(List<ClusterDependency> dependencies)
    {
        var depsPath = _fileSystem.Path.Combine(_workspaceProvider.Cache, IFileSystemExtensions.DependenciesDirectory);
        if (!_fileSystem.Path.Exists(depsPath))
        {
            _fileSystem.AddDirectory(depsPath);
        }

        foreach (var dependency in dependencies)
        {
            var versionDir = _fileSystem.Path.Combine(depsPath, dependency.Name, dependency.Version.ToString());
            _fileSystem.AddDirectory(versionDir);

            var emptyDll = _fileSystem.Path.Combine(versionDir, $"{dependency.Name}.dll");
            var emptyDepsJson = _fileSystem.Path.Combine(versionDir, $"{dependency.Name}.deps.json");
            _fileSystem.AddEmptyFile(emptyDll);
            _fileSystem.AddEmptyFile(emptyDepsJson);
        }
    }

    private string SetupUpdateQueueFile(List<ClusterDependencyOperation> queuedUpdates)
    {
        var updateFile = _fileSystem.Path.Combine(HomePath, QueueFileName);
        var updateJson = JsonSerializer.Serialize(queuedUpdates, DefaultJsonSerializerSettings.Default);
        _fileSystem.AddFile(updateFile, new MockFileData(updateJson));

        return updateFile;
    }

    private async Task<List<ClusterDependency>?> DeserializeClusterDependencies(string jsonFilePath)
    {
        var fileContent = await _fileSystem.File.ReadAllTextAsync(jsonFilePath);
        return JsonSerializer.Deserialize<List<ClusterDependency>>(fileContent, DefaultJsonSerializerSettings.Default);
    }

    private async Task<List<ClusterDependencyOperation>?> DeserializeClusterDependencyOperations(string jsonFilePath)
    {
        var fileContent = await _fileSystem.File.ReadAllTextAsync(jsonFilePath);
        return JsonSerializer.Deserialize<List<ClusterDependencyOperation>>(fileContent, DefaultJsonSerializerSettings.Default);
    }
}
