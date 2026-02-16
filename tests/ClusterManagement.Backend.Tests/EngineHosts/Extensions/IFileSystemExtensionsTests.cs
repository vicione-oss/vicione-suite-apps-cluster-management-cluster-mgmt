using System.IO.Abstractions.TestingHelpers;
using ClusterManagement.Backend.EngineHosts.Extensions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Internal;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Tests.EngineHosts.Extensions;

public class IFileSystemExtensionsTests
{
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();

    public sealed class GetEngineHostInstallDirectory : IFileSystemExtensionsTests
    {
        [Fact]
        public void Should_return_engine_host_version_path()
        {
            // Arrange
            MockFileSystem fileSystem = new();
            _workspaceProvider.Cache.Returns("workspace");
            ClusterManagementOptions options = new();

            // Act
            var installDirectory = fileSystem.GetEngineHostInstallDirectory(_workspaceProvider, options);

            // Assert
            var expected = fileSystem.Path.Combine(
            [
                fileSystem.GetEngineHostVersionsPath(_workspaceProvider),
                options.GetEngineHostPackageName(),
            ]);
            installDirectory.Should().Be(expected);
        }
    }
}
