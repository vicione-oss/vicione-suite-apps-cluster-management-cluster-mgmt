using System.IO.Abstractions.TestingHelpers;
using ClusterManagement.Backend.Iodds;
using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Tests.Iodds;

public class IoddUploadCleanupTests
{
    private static readonly string s_cacheDirectory = MockUnixSupport.Path(@"c:\cache");
    private static readonly string s_uploadDirectory = Path.Combine(s_cacheDirectory, IoddUpload.DirectoryName);

    private readonly MockFileSystem _fileSystem = new();
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>();
    private readonly IoddUploadCleanup _cleanup;

    public IoddUploadCleanupTests()
    {
        _workspaceProvider.Cache.Returns(s_cacheDirectory);
        _cleanup = new IoddUploadCleanup(_workspaceProvider, _fileSystem, Substitute.For<ILogger<IoddUploadCleanup>>());
    }

    [Fact]
    public void Should_delete_stale_uploads_and_keep_recent_ones()
    {
        // Arrange
        var staleUpload = Path.Combine(s_uploadDirectory, "stale.zip");
        var recentUpload = Path.Combine(s_uploadDirectory, "recent.zip");

        _fileSystem.AddFile(staleUpload, new MockFileData("stale"));
        _fileSystem.AddFile(recentUpload, new MockFileData("recent"));
        _fileSystem.File.SetLastWriteTimeUtc(staleUpload, DateTime.UtcNow - TimeSpan.FromHours(1));

        // Act
        _cleanup.CleanupLeftovers(TimeSpan.FromMinutes(5));

        // Assert
        _fileSystem.File.Exists(staleUpload).Should().BeFalse();
        _fileSystem.File.Exists(recentUpload).Should().BeTrue();
    }

    [Fact]
    public void Should_not_touch_files_outside_the_upload_directory()
    {
        // Arrange
        var outsideFile = Path.Combine(s_cacheDirectory, "other.zip");

        _fileSystem.AddFile(outsideFile, new MockFileData("other"));
        _fileSystem.File.SetLastWriteTimeUtc(outsideFile, DateTime.UtcNow - TimeSpan.FromHours(1));
        _fileSystem.AddDirectory(s_uploadDirectory);

        // Act
        _cleanup.CleanupLeftovers(TimeSpan.FromMinutes(5));

        // Assert
        _fileSystem.File.Exists(outsideFile).Should().BeTrue();
    }

    [Fact]
    public void Should_not_throw_if_upload_directory_is_missing()
    {
        // Act
        var act = () => _cleanup.CleanupLeftovers(TimeSpan.FromMinutes(5));

        // Assert
        act.Should().NotThrow();
    }
}
