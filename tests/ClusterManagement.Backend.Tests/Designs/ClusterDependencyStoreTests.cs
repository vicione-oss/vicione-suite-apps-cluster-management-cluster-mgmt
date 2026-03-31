using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Internal;
using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Backend.Modules;
using Sdk.Messaging;
using ViciOne.Cluster.Model;

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

            var package1 = new ClusterDependency { Name = "Test.Package1", Version = "1.0.0" };
            var package2 = new ClusterDependency { Name = "Test.Package2", Version = "2.2.0" };
            var package3 = new ClusterDependency { Name = "Test.Package3", Version = "1.0.11" };
            var package4 = new ClusterDependency { Name = "Test.Package4", Version = "3.0.0-ci123456789" };

            SetupStoredPackageVersions([package1, package2, package3, package4]);

            // Act
            var result = store.BuildStoredPackageVersionMap();

            // Assert
            result.Should().HaveCount(4);
            result.Should().Contain(d => d.Key == package1.Name && d.Value.Any(v => v == package1.Version));
            result.Should().Contain(d => d.Key == package2.Name && d.Value.Any(v => v == package2.Version));
            result.Should().Contain(d => d.Key == package3.Name && d.Value.Any(v => v == package3.Version));
            result.Should().Contain(d => d.Key == package4.Name && d.Value.Any(v => v == package4.Version));
        }

        [Fact]
        public void Should_allow_multiple_package_versions()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();

            var package1 = new ClusterDependency { Name = "Test.Package1", Version = "1.0.0" };
            var package2 = new ClusterDependency { Name = "Test.Package1", Version = "2.2.0" };
            var package3 = new ClusterDependency { Name = "Test.Package1", Version = "1.0.11" };
            var package4 = new ClusterDependency { Name = "Test.Package1", Version = "3.0.0-ci123456789" };

            SetupStoredPackageVersions([package1, package2, package3, package4]);

            // Act
            var result = store.BuildStoredPackageVersionMap();

            // Assert
            result.Should().HaveCount(1);
            result.Should().Contain(d => d.Key == package1.Name && d.Value.Count() == 4);
        }
    }

    public sealed class Initialize : ClusterDependencyStoreTests
    {
        [Fact]
        public async Task Should_create_packages_file_from_embedded_resources_when_no_packages_file_exists()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();

            // Act
            await store.Initialize(CancellationToken.None);

            // Assert
            var packagesFile = _fileSystem.Path.Combine(HomePath, $"{ClusterDependencyStore.PackagesFileName}.{ClusterDependencyStore.PackagesFileExtension}");
            _fileSystem.File.Exists(packagesFile).Should().BeTrue();
        }

        [Fact]
        public async Task Should_queue_install_when_shipped_version_is_higher_than_existing()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            SetupPackagesFile([new ClusterDependency { Name = "ViciOne.Engine.DefaultPoolings", Version = "0.9.0" }]);

            // Act
            await store.Initialize(CancellationToken.None);

            // Assert
            var enqueuedOps = await store.GetEnqueuedOperations(CancellationToken.None);
            enqueuedOps.Should().ContainSingle(op =>
                op.Dependency.Name == "ViciOne.Engine.DefaultPoolings"
                && op.OperationKind == ClusterDependencyOperationKind.Install
                && op.Dependency.Version == "1.0.0");
        }

        [Fact]
        public async Task Should_not_queue_update_when_existing_version_matches_shipped()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            SetupPackagesFile([new ClusterDependency { Name = "ViciOne.Engine.DefaultPoolings", Version = "1.0.0" }]);

            // Act
            await store.Initialize(CancellationToken.None);

            // Assert
            var enqueuedOps = await store.GetEnqueuedOperations(CancellationToken.None);
            enqueuedOps.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_not_queue_update_when_existing_version_is_higher_than_shipped()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            SetupPackagesFile([new ClusterDependency { Name = "ViciOne.Engine.DefaultPoolings", Version = "2.0.0" }]);

            // Act
            await store.Initialize(CancellationToken.None);

            // Assert
            var enqueuedOps = await store.GetEnqueuedOperations(CancellationToken.None);
            enqueuedOps.Should().BeEmpty();
        }

        [Fact]
        public async Task Should_not_queue_update_for_packages_not_in_shipped_set()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            SetupPackagesFile([new ClusterDependency { Name = "My.Custom.Package", Version = "1.0.0" }]);

            // Act
            await store.Initialize(CancellationToken.None);

            // Assert
            var enqueuedOps = await store.GetEnqueuedOperations(CancellationToken.None);
            enqueuedOps.Should().BeEmpty();
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
                new() { Name = "TestPackage1", Version = "1.0.0" },
                new() { Name = "TestPackage2", Version = "2.0.0" },
                new() { Name = "TestPackage3", Version = "3.0.0-ci123456789" }
            ]);

            // Act
            var result = await store.LoadDependencies(CancellationToken.None);

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain(d => d.Name == "TestPackage1" && d.Version == "1.0.0");
            result.Should().Contain(d => d.Name == "TestPackage2" && d.Version == "2.0.0");
            result.Should().Contain(d => d.Name == "TestPackage3" && d.Version == "3.0.0-ci123456789");
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
                new() { Name = "EdgePackage", Version = "1.0.0" }
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
            var dependency = new ClusterDependency() { Name = "TestPackage", Version = "1.0.0" };

            SetupPackagesFile([]);

            var operations = new List<ClusterDependencyOperation>
            {
                new(dependency, ClusterDependencyOperationKind.Install)
            };

            // Act
            _ = await store.EnqueueOperations(operations, CancellationToken.None);

            // Assert
            var updateFile = _fileSystem.Path.Combine(HomePath, QueueFileName);
            _fileSystem.File.Exists(updateFile).Should().BeTrue();

            var queued = await DeserializeClusterDependencyOperations(updateFile);

            queued.Should().HaveCount(1);
            queued.Should().Contain(d => d.Dependency == dependency && d.OperationKind == ClusterDependencyOperationKind.Install);
        }

        [Fact]
        public async Task Should_merge_with_existing_queued_operations()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            var queuedDependency = new ClusterDependency() { Name = "QueuedPackage", Version = "1.0.0" };
            var newDependency = new ClusterDependency() { Name = "NewPackage", Version = "2.0.0" };
            var otherDependency = new ClusterDependency() { Name = "OtherPackage", Version = "1.4.2-ci235698741" };

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
            var changes = await store.EnqueueOperations(operations, CancellationToken.None);

            // Assert
            changes.Should().HaveCount(2).And.AllSatisfy(k => k.Action.Should().Be(CrudAction.Created));

            var queued = await DeserializeClusterDependencyOperations(updateFile);

            queued.Should().HaveCount(3);
            queued.Should().Contain(d => d.Dependency == queuedDependency && d.OperationKind == ClusterDependencyOperationKind.Install);
            queued.Should().Contain(d => d.Dependency == newDependency && d.OperationKind == ClusterDependencyOperationKind.Install);
            queued.Should().Contain(d => d.Dependency == otherDependency && d.OperationKind == ClusterDependencyOperationKind.Install);
        }

        [Fact]
        public async Task Should_overwrite_existing_package_with_same_name()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            var existingDependency = new ClusterDependency() { Name = "TestPackage", Version = "1.0.0" };
            var overwriteDependency = new ClusterDependency() { Name = "TestPackage", Version = "2.0.0" };

            SetupPackagesFile([]);

            var updateFile = SetupUpdateQueueFile([
                new ClusterDependencyOperation(existingDependency, ClusterDependencyOperationKind.Install)
            ]);

            var operations = new List<ClusterDependencyOperation>
            {
                new(overwriteDependency, ClusterDependencyOperationKind.Install)
            };

            // Act
            var changes = await store.EnqueueOperations(operations, CancellationToken.None);

            // Assert
            var queued = await DeserializeClusterDependencyOperations(updateFile);

            changes.Should().HaveCount(1);
            changes.Should().Contain(d => d.Operation.Dependency == overwriteDependency
                && d.Operation.OperationKind == ClusterDependencyOperationKind.Install);
        }

        [Fact]
        public async Task Should_not_create_file_when_no_dependencies_to_queue()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            var operations = new List<ClusterDependencyOperation>();

            // Act
            var changes = await store.EnqueueOperations(operations, CancellationToken.None);

            // Assert
            changes.Should().BeEmpty();
            var updateFile = _fileSystem.Path.Combine(HomePath, QueueFileName);
            _fileSystem.File.Exists(updateFile).Should().BeFalse();
        }

        [Fact]
        public async Task Should_remove_redundant_install_package_operations()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            var installedDependency = new ClusterDependency { Name = "TestPackage", Version = "1.0.0" };

            SetupPackagesFile([installedDependency]);

            var operations = new List<ClusterDependencyOperation>
            {
                new(installedDependency, ClusterDependencyOperationKind.Install)
            };

            // Act
            var changes = await store.EnqueueOperations(operations, CancellationToken.None);

            // Assert            
            var updateFile = _fileSystem.Path.Combine(HomePath, QueueFileName);
            _fileSystem.File.Exists(updateFile).Should().BeFalse();

            changes.Should().Contain(k => k.Action == CrudAction.Deleted
                && k.Operation.Dependency.Name == installedDependency.Name
                && k.Operation.OperationKind == ClusterDependencyOperationKind.Install);
        }

        [Fact]
        public async Task Should_remove_redundant_uninstall_package_operations()
        {
            // Arrange
            var store = _services.GetRequiredService<IClusterDependencyStore>();
            var installedDependency = new ClusterDependency { Name = "TestPackage", Version = "1.0.0" };
            var deleteDependency = new ClusterDependency() { Name = "TestPackage", Version = "2.0.0" };

            SetupPackagesFile([installedDependency]);

            var operations = new List<ClusterDependencyOperation>
            {
                new(deleteDependency, ClusterDependencyOperationKind.Uninstall)
            };

            // Act
            var changes = await store.EnqueueOperations(operations, CancellationToken.None);

            // Assert
            changes.Should().Contain(k => k.Action == CrudAction.Deleted
                && k.Operation.Dependency == deleteDependency);

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
            var existingDependency = new ClusterDependency() { Name = "ExistingPackage", Version = "1.0.0" };
            var newDependency = new ClusterDependency() { Name = "NewPackage", Version = "2.0.0" };

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
            var existingDependency = new ClusterDependency() { Name = "TestPackage", Version = "1.0.0" };
            var updateDependency = new ClusterDependency() { Name = "TestPackage", Version = "2.0.0" };

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
            var existingDependency = new ClusterDependency() { Name = "TestPackage", Version = "1.0.0" };
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
            var existingDependency = new ClusterDependency() { Name = "TestPackage", Version = "1.0.0" };
            var toBeRemovedDependency = new ClusterDependency() { Name = "ToBeRemovedPackage", Version = "1.0.0" };

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
            var existingDependency = new ClusterDependency() { Name = "TestPackage", Version = "1.0.0" };
            var updateDependency = new ClusterDependency() { Name = "TestPackage", Version = "2.0.0" };

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
