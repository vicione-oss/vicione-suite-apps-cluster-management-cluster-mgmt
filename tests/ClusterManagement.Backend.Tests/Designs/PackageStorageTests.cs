using System.IO.Abstractions;
using System.IO.Compression;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Internal;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using ClusterManagement.Tests.Tools.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using Sdk.Testing;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests.Designs;

public class PackageStorageTests
{
    private const string ValidZipFilename = "valid-package.zip";
    private const string InvalidZipFilename = "invalid-package.zip";

    private readonly IClusterDependencyStore _dependencyStore = Substitute.For<IClusterDependencyStore>();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceService = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
    private readonly ILogger<PackageStore> _logger = Substitute.For<ILogger<PackageStore>>();

    public PackageStorageTests()
        => _dependencyStore.LoadDependencies(Arg.Any<CancellationToken>()).Returns([]);

    private static TemporaryDirectory SetupValidArchiveFileSystem(IFileSystem fileSystem, ClusterDependency dependency, out string validPackagePath)
    {
        var sourceDirectory = new TemporaryDirectory();
        var sourceZipBytes = PackageFactory.CreatePackageArchiveFromDependency(dependency.Name);
        validPackagePath = fileSystem.Path.Combine(sourceDirectory.Path, ValidZipFilename);

        fileSystem.File.WriteAllBytes(validPackagePath, sourceZipBytes);

        return sourceDirectory;
    }

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
            var fileSystem = services.GetRequiredService<IFileSystem>();
            var dependency = PackageFactory.ConstantPackageDependency;

            using var sourceDirectory = SetupValidArchiveFileSystem(fileSystem, dependency, out var validPackagePath);
            using var tempDirectory = new TemporaryDirectory();
            var destinationPath = fileSystem.Path.Combine(tempDirectory.Path, "ViciOne.Suite.ClusterManagement");
            var store = services.GetRequiredService<IPackageStore>();

            _workspaceService.Cache.Returns(destinationPath);

            // Act
            await store.StorePackage(validPackagePath, true, CancellationToken.None);

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
            var fileSystem = services.GetRequiredService<IFileSystem>();
            var constantPackage = PackageFactory.ConstantPackageDependency;

            using var sourceDirectory = SetupValidArchiveFileSystem(fileSystem, constantPackage, out var validPackagePath);
            using var tempDirectory = new TemporaryDirectory();
            var destinationPath = fileSystem.Path.Combine(tempDirectory.Path, "ViciOne.Suite.ClusterManagement");
            var store = services.GetRequiredService<IPackageStore>();

            _workspaceService.Cache.Returns(destinationPath);

            // Act
            var dependency = await store.StorePackage(validPackagePath, true, CancellationToken.None);

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
            var fileSystem = services.GetRequiredService<IFileSystem>();
            var constantPackage = PackageFactory.ConstantPackageDependency;

            using var sourceDirectory = SetupValidArchiveFileSystem(fileSystem, constantPackage, out var validPackagePath);
            using var tempDirectory = new TemporaryDirectory();
            var destinationPath = fileSystem.Path.Combine(tempDirectory.Path, "ViciOne.Suite.ClusterManagement");
            var store = services.GetRequiredService<IPackageStore>();

            _workspaceService.Cache.Returns(destinationPath);

            // Act
            var dependency = await store.StorePackage(validPackagePath, true, CancellationToken.None);

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
            var fileSystem = services.GetRequiredService<IFileSystem>();
            using var sourceDirectory = new TemporaryDirectory();
            var sourceZipBytes = PackageFactory.CreateInvalidPackageZipArchiveBytes("ViciOne.Test.Package");
            var invalidPackagePath = fileSystem.Path.Combine(sourceDirectory.Path, InvalidZipFilename);

            await fileSystem.File.WriteAllBytesAsync(invalidPackagePath, sourceZipBytes, TestContext.Current.CancellationToken);

            // Act
            var act = () => store.StorePackage(invalidPackagePath, true, CancellationToken.None);

            // Assert
            (await act.Should().ThrowAsync<PackageValidationException>()
                .WithMessage("The provided package archive does not contain a deps.json file."))
                .Which.ErrorCode.Should().Be(ClusterPackageChanged.InvalidPackageContent);
        }

        [Fact]
        public async Task Should_throw_if_we_can_get_aversion_from_deps_json_file()
        {
            // Arrange
            using var services = CreateServices();
            var store = services.GetRequiredService<IPackageStore>();
            var fileSystem = services.GetRequiredService<IFileSystem>();
            using var sourceDirectory = new TemporaryDirectory();
            var sourceZipBytes = PackageFactory.CreatePackageZipArchiveBytes("ViciOne.Test.Package", "0.46.4", string.Empty);
            var invalidPackagePath = fileSystem.Path.Combine(sourceDirectory.Path, InvalidZipFilename);
            await fileSystem.File.WriteAllBytesAsync(invalidPackagePath, sourceZipBytes, TestContext.Current.CancellationToken);

            // Act
            var act = () => store.StorePackage(invalidPackagePath, true, CancellationToken.None);

            // Assert
            (await act.Should().ThrowAsync<PackageValidationException>()
                .WithMessage("Package version of 'ViciOne.Test.Package' not found in deps.json."))
                .Which.ErrorCode.Should().Be(ClusterPackageChanged.InvalidPackageContent);
        }

        [Fact]
        public async Task Should_throw_if_file_is_not_a_zip_archive()
        {
            // Arrange
            using var services = CreateServices();
            var store = services.GetRequiredService<IPackageStore>();
            var fileSystem = services.GetRequiredService<IFileSystem>();
            using var sourceDirectory = new TemporaryDirectory();
            var invalidPackagePath = fileSystem.Path.Combine(sourceDirectory.Path, InvalidZipFilename);

            await fileSystem.File.WriteAllTextAsync(invalidPackagePath, "this is not a zip archive", TestContext.Current.CancellationToken);

            // Act
            var act = () => store.StorePackage(invalidPackagePath, true, CancellationToken.None);

            // Assert
            (await act.Should().ThrowAsync<PackageValidationException>())
                .Which.ErrorCode.Should().Be(ClusterPackageChanged.NotAZipArchive);
        }

        [Fact]
        public async Task Should_throw_if_archive_is_empty()
        {
            // Arrange
            using var services = CreateServices();
            var store = services.GetRequiredService<IPackageStore>();
            var fileSystem = services.GetRequiredService<IFileSystem>();
            using var sourceDirectory = new TemporaryDirectory();
            var emptyPackagePath = fileSystem.Path.Combine(sourceDirectory.Path, InvalidZipFilename);

            await using (var stream = fileSystem.File.Create(emptyPackagePath))
            using (new ZipArchive(stream, ZipArchiveMode.Create))
            {
            }

            // Act
            var act = () => store.StorePackage(emptyPackagePath, true, CancellationToken.None);

            // Assert
            (await act.Should().ThrowAsync<PackageValidationException>())
                .Which.ErrorCode.Should().Be(ClusterPackageChanged.EmptyArchive);
        }

        [Fact]
        public async Task Should_throw_and_remove_partial_extraction_if_archive_is_damaged()
        {
            // Arrange
            using var services = CreateServices();
            var fileSystem = services.GetRequiredService<IFileSystem>();
            var dependency = PackageFactory.ConstantPackageDependency;

            using var sourceDirectory = SetupValidArchiveFileSystem(fileSystem, dependency, out var packagePath);
            using var tempDirectory = new TemporaryDirectory();
            var destinationPath = fileSystem.Path.Combine(tempDirectory.Path, "ViciOne.Suite.ClusterManagement");
            var store = services.GetRequiredService<IPackageStore>();

            _workspaceService.Cache.Returns(destinationPath);
            AppendCorruptEntry(fileSystem, packagePath);

            // Act
            var act = () => store.StorePackage(packagePath, true, CancellationToken.None);

            // Assert
            (await act.Should().ThrowAsync<PackageValidationException>())
                .Which.ErrorCode.Should().Be(ClusterPackageChanged.CorruptArchive);

            var packageVersionPath = fileSystem.Path.Combine(destinationPath, IFileSystemExtensions.DependenciesDirectory, dependency.Name, dependency.Version.ToString());
            fileSystem.Directory.Exists(packageVersionPath).Should().BeFalse();
        }

        [Fact]
        public async Task Should_replace_existing_package_version()
        {
            // Arrange
            using var services = CreateServices();
            var fileSystem = services.GetRequiredService<IFileSystem>();
            var dependency = PackageFactory.ConstantPackageDependency;

            using var sourceDirectory = SetupValidArchiveFileSystem(fileSystem, dependency, out var packagePath);
            using var tempDirectory = new TemporaryDirectory();
            var destinationPath = fileSystem.Path.Combine(tempDirectory.Path, "ViciOne.Suite.ClusterManagement");
            var store = services.GetRequiredService<IPackageStore>();

            _workspaceService.Cache.Returns(destinationPath);
            await store.StorePackage(packagePath, true, CancellationToken.None);

            var packageVersionPath = fileSystem.Path.Combine(destinationPath, IFileSystemExtensions.DependenciesDirectory, dependency.Name, dependency.Version.ToString());
            var outdatedFile = fileSystem.Path.Combine(packageVersionPath, "outdated.txt");
            await fileSystem.File.WriteAllTextAsync(outdatedFile, "from the replaced version", TestContext.Current.CancellationToken);

            // Act
            await store.StorePackage(packagePath, true, CancellationToken.None);

            // Assert: the new version is in place, the replaced one is gone completely
            fileSystem.Directory.GetFiles(packageVersionPath, "*.deps.json").Should().NotBeEmpty();
            fileSystem.File.Exists(outdatedFile).Should().BeFalse();

            var replacedPath = fileSystem.Path.Combine(destinationPath, IFileSystemExtensions.ReplacedDependenciesDirectory);
            fileSystem.Directory.EnumerateFileSystemEntries(replacedPath).Should().BeEmpty();

            var stagingPath = fileSystem.Path.Combine(destinationPath, IFileSystemExtensions.PackageStagingDirectory);
            fileSystem.Directory.EnumerateFileSystemEntries(stagingPath).Should().BeEmpty();
        }

        [Fact]
        public async Task Should_throw_version_in_use_if_version_is_installed()
        {
            // Arrange
            using var services = CreateServices();
            var fileSystem = services.GetRequiredService<IFileSystem>();
            var dependency = PackageFactory.ConstantPackageDependency;

            using var sourceDirectory = SetupValidArchiveFileSystem(fileSystem, dependency, out var packagePath);
            using var tempDirectory = new TemporaryDirectory();
            var destinationPath = fileSystem.Path.Combine(tempDirectory.Path, "ViciOne.Suite.ClusterManagement");
            var store = services.GetRequiredService<IPackageStore>();

            _workspaceService.Cache.Returns(destinationPath);
            await store.StorePackage(packagePath, true, CancellationToken.None);

            var packageVersionPath = fileSystem.Path.Combine(destinationPath, IFileSystemExtensions.DependenciesDirectory, dependency.Name, dependency.Version.ToString());
            var installedFile = fileSystem.Path.Combine(packageVersionPath, "installed.txt");
            await fileSystem.File.WriteAllTextAsync(installedFile, "from the installed version", TestContext.Current.CancellationToken);

            _dependencyStore.LoadDependencies(Arg.Any<CancellationToken>())
                .Returns([new ClusterDependency { Name = dependency.Name, Version = dependency.Version }]);

            // Act
            var act = () => store.StorePackage(packagePath, true, CancellationToken.None);

            // Assert
            (await act.Should().ThrowAsync<PackageValidationException>())
                .Which.ErrorCode.Should().Be(ClusterPackageChanged.PackageVersionInUse);

            fileSystem.File.Exists(installedFile).Should().BeTrue();
        }

        [Fact]
        public async Task Should_restore_replaced_package_version_if_new_one_is_damaged()
        {
            // Arrange
            using var services = CreateServices();
            var fileSystem = services.GetRequiredService<IFileSystem>();
            var dependency = PackageFactory.ConstantPackageDependency;

            using var sourceDirectory = SetupValidArchiveFileSystem(fileSystem, dependency, out var packagePath);
            using var tempDirectory = new TemporaryDirectory();
            var destinationPath = fileSystem.Path.Combine(tempDirectory.Path, "ViciOne.Suite.ClusterManagement");
            var store = services.GetRequiredService<IPackageStore>();

            _workspaceService.Cache.Returns(destinationPath);
            await store.StorePackage(packagePath, true, CancellationToken.None);

            var packageVersionPath = fileSystem.Path.Combine(destinationPath, IFileSystemExtensions.DependenciesDirectory, dependency.Name, dependency.Version.ToString());
            var installedFiles = fileSystem.Directory.GetFiles(packageVersionPath, "*", SearchOption.AllDirectories);

            AppendCorruptEntry(fileSystem, packagePath);

            // Act
            var act = () => store.StorePackage(packagePath, true, CancellationToken.None);

            // Assert
            (await act.Should().ThrowAsync<PackageValidationException>())
                .Which.ErrorCode.Should().Be(ClusterPackageChanged.CorruptArchive);

            fileSystem.Directory.GetFiles(packageVersionPath, "*", SearchOption.AllDirectories).Should().BeEquivalentTo(installedFiles);

            // A failed extraction never touches the existing version, and leaves nothing staged behind.
            fileSystem.Directory.Exists(fileSystem.Path.Combine(destinationPath, IFileSystemExtensions.ReplacedDependenciesDirectory)).Should().BeFalse();
            fileSystem.Directory.EnumerateFileSystemEntries(fileSystem.Path.Combine(destinationPath, IFileSystemExtensions.PackageStagingDirectory)).Should().BeEmpty();
        }

        /// <summary>
        /// Appends an extra entry and damages its compressed data. The archive can still be opened and verified
        /// (deps.json and dll stay intact), but extracting the damaged entry fails.
        /// </summary>
        private static void AppendCorruptEntry(IFileSystem fileSystem, string packagePath)
        {
            const string CorruptEntryName = "zz-corrupt-entry.txt";

            using (var stream = fileSystem.File.Open(packagePath, FileMode.Open, FileAccess.ReadWrite))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Update))
            using (var writer = new StreamWriter(archive.CreateEntry(CorruptEntryName, CompressionLevel.Optimal).Open()))
            {
                for (var i = 0; i < 1000; i++)
                    writer.WriteLine($"line {i} of an entry that gets damaged");
            }

            // The first occurrence of the name is in the entry's local file header (the central directory follows at the end).
            var bytes = fileSystem.File.ReadAllBytes(packagePath);
            var nameBytes = System.Text.Encoding.ASCII.GetBytes(CorruptEntryName);
            var nameOffset = bytes.AsSpan().IndexOf(nameBytes);
            var extraFieldLength = BitConverter.ToUInt16(bytes, nameOffset - 2);
            var dataOffset = nameOffset + nameBytes.Length + extraFieldLength;

            for (var i = 0; i < 16; i++)
                bytes[dataOffset + 8 + i] ^= 0xFF;

            fileSystem.File.WriteAllBytes(packagePath, bytes);
        }
    }

    public sealed class CleanupLeftovers : PackageStorageTests
    {
        [Fact]
        public void Should_remove_replaced_and_staged_packages_and_stale_uploads()
        {
            // Arrange
            using var services = CreateServices();
            var fileSystem = services.GetRequiredService<IFileSystem>();
            using var tempDirectory = new TemporaryDirectory();
            var store = services.GetRequiredService<IPackageStore>();

            _workspaceService.Cache.Returns(tempDirectory.Path);

            var replacedPath = fileSystem.Path.Combine(tempDirectory.Path, IFileSystemExtensions.ReplacedDependenciesDirectory, "leftover");
            var stagingPath = fileSystem.Path.Combine(tempDirectory.Path, IFileSystemExtensions.PackageStagingDirectory, "leftover");
            var uploadPath = fileSystem.Path.Combine(tempDirectory.Path, ClusterPackageUpload.DirectoryName);
            var staleUpload = fileSystem.Path.Combine(uploadPath, "stale.zip");
            var recentUpload = fileSystem.Path.Combine(uploadPath, "recent.zip");

            fileSystem.Directory.CreateDirectory(replacedPath);
            fileSystem.Directory.CreateDirectory(stagingPath);
            fileSystem.Directory.CreateDirectory(uploadPath);
            fileSystem.File.WriteAllText(fileSystem.Path.Combine(replacedPath, "file.dll"), "replaced");
            fileSystem.File.WriteAllText(staleUpload, "stale");
            fileSystem.File.WriteAllText(recentUpload, "recent");
            fileSystem.File.SetLastWriteTimeUtc(staleUpload, DateTime.UtcNow - TimeSpan.FromHours(1));

            // Act
            store.CleanupLeftovers(TimeSpan.FromMinutes(5));

            // Assert
            fileSystem.Directory.Exists(fileSystem.Path.Combine(tempDirectory.Path, IFileSystemExtensions.ReplacedDependenciesDirectory)).Should().BeFalse();
            fileSystem.Directory.Exists(fileSystem.Path.Combine(tempDirectory.Path, IFileSystemExtensions.PackageStagingDirectory)).Should().BeFalse();
            fileSystem.File.Exists(staleUpload).Should().BeFalse();
            fileSystem.File.Exists(recentUpload).Should().BeTrue();
        }

        [Fact]
        public void Should_not_throw_if_nothing_to_clean_up()
        {
            // Arrange
            using var services = CreateServices();
            using var tempDirectory = new TemporaryDirectory();
            var store = services.GetRequiredService<IPackageStore>();

            _workspaceService.Cache.Returns(tempDirectory.Path);

            // Act
            var act = () => store.CleanupLeftovers(TimeSpan.FromMinutes(5));

            // Assert
            act.Should().NotThrow();
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
                Version = "0.46.4"
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
            fileSystem.Directory.Received(0).Delete(path: Arg.Is(packagePath), Arg.Any<bool>());
        }

        [Fact]
        public async Task Should_enqueue_uninstall_operation_if_dependency_exists()
        {
            // Arrange            
            var fileSystem = Substitute.For<IFileSystem>();
            using var services = CreateServices(fileSystem);
            var store = services.GetRequiredService<IPackageStore>();
            var dependency = new ClusterDependency
            {
                Name = "ViciOne.Test.Package",
                Version = "0.46.4"
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
