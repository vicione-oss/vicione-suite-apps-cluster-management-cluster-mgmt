using System.IO.Abstractions;
using System.IO.Compression;
using AwesomeAssertions;
using ClusterManagement.Backend.Designs;
using ClusterManagement.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sdk.Backend.Modules;
using Xunit;

namespace ClusterManagement.Backend.Tests.Designs;

public class PackageStorageTests
{
    private readonly IFileSystem _fileSystem = new FileSystem();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceService = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
    private readonly ILogger<DesignLoader> _logger = Substitute.For<ILogger<DesignLoader>>();
    private readonly IServiceProvider _services;


    public PackageStorageTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<ClusterManagementOptions>()
            .AddSingleton<IPackagesStore, InMemoryPackagesStore>()
            .AddSingleton(_fileSystem)
            .AddSingleton(_workspaceService)
            .AddTransient(_ => _logger)
            .BuildServiceProvider();
    }

    public sealed class TryAddDevelopmentPackage : PackageStorageTests
    {
        [Fact]
        [Trait(Traits.Category, Traits.System)]
        public async Task Should_extract_name_and_version_from_archive()
        {
            // Arrange                        
            var destinationPath = "Path\\To\\Cache\\ViciOne.Suite-ClusterManagement";
            var packageName = "ViciOne.Test.Package";
            var packageVersion = "0.46.4";

            var sourceZipBytes = CreateTestZipArchiveBytes("ViciOne.Test.Package", "0.46.4");
            var store = _services.GetRequiredService<IPackagesStore>();

            _workspaceService.Cache.Returns(destinationPath);

            // Act
            await store.AddPackageManually(sourceZipBytes, CancellationToken.None);

            // Assert
            var expectedFbPath = _fileSystem.Path.Combine(destinationPath, "Dependencies", packageName);
            _fileSystem.Directory.Exists(expectedFbPath).Should().BeTrue();
            _fileSystem.Directory.Exists(_fileSystem.Path.Combine(expectedFbPath, packageVersion)).Should().BeTrue();
        }

        private static byte[] CreateTestZipArchiveBytes(string packageName, string version)
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                // Add package.json or manifest
                var manifestEntry = archive.CreateEntry("package.json", CompressionLevel.Fastest);
                using (var entryStream = manifestEntry.Open())
                using (var writer = new StreamWriter(entryStream))
                {
                    writer.Write($$"""{"name":"{{packageName}}","version":"{{version}}"}""");
                }

                // Add other required files
                var dllEntry = archive.CreateEntry($"{packageName}.dll", CompressionLevel.Fastest);
                using (var entryStream = dllEntry.Open())
                {
                    entryStream.Write([0x4D, 0x5A]); // PE header magic
                }
            }

            return memoryStream.ToArray();
        }
    }
}
