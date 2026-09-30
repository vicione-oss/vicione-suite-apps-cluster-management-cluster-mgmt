using System.IO.Abstractions.TestingHelpers;
using ClusterManagement.Backend.Extensions;

namespace ClusterManagement.Backend.Tests.Extensions;

public class IFileSystemUploadExtensionsTests
{
    private static readonly string s_directory = MockUnixSupport.Path(@"c:\cache\upload");

    private readonly MockFileSystem _fileSystem = new();

    [Fact]
    public void Should_accept_a_file_directly_inside_the_directory()
        => _fileSystem.IsFileDirectlyInDirectory(s_directory, Path.Combine(s_directory, "file.zip")).Should().BeTrue();

    [Theory]
    [InlineData(@"c:\cache\upload\sub\file.zip")]
    [InlineData(@"c:\cache\upload\..\file.zip")]
    [InlineData(@"c:\cache\other\file.zip")]
    [InlineData(@"c:\cache\upload")]
    public void Should_reject_a_file_not_directly_inside_the_directory(string filePath)
        => _fileSystem.IsFileDirectlyInDirectory(s_directory, MockUnixSupport.Path(filePath)).Should().BeFalse();

    [Fact]
    public void Should_delete_old_files_and_keep_recent_ones()
    {
        // Arrange
        var oldFile = Path.Combine(s_directory, "old.zip");
        var recentFile = Path.Combine(s_directory, "recent.zip");
        List<string> deleting = [];

        _fileSystem.AddFile(oldFile, new MockFileData("old"));
        _fileSystem.AddFile(recentFile, new MockFileData("recent"));
        _fileSystem.File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow - TimeSpan.FromHours(1));

        // Act
        _fileSystem.DeleteFilesOlderThan(s_directory, TimeSpan.FromMinutes(5), deleting.Add, (_, _) => { });

        // Assert
        _fileSystem.File.Exists(oldFile).Should().BeFalse();
        _fileSystem.File.Exists(recentFile).Should().BeTrue();
        deleting.Should().Equal(oldFile);
    }

    [Fact]
    public void Should_ignore_a_missing_directory()
    {
        // Act
        var act = () => _fileSystem.DeleteFilesOlderThan(s_directory, TimeSpan.FromMinutes(5), _ => { }, (_, _) => { });

        // Assert
        act.Should().NotThrow();
    }
}
