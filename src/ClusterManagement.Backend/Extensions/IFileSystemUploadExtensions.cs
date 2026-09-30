using System.IO.Abstractions;

namespace ClusterManagement.Backend.Extensions;

/// <summary>
/// File helpers shared by the package and IODD uploads, so both check and clean up their upload directories the same way.
/// </summary>
// TODO: Replace with the SDK helpers once vicione-oss/vicione/suite/suite-sdk#361 provides them.
internal static class IFileSystemUploadExtensions
{
    /// <summary>
    /// Returns whether <paramref name="filePath"/> names a file directly inside <paramref name="directory"/>.
    /// </summary>
    public static bool IsFileDirectlyInDirectory(this IFileSystem fileSystem, string directory, string filePath)
    {
        var fullPath = fileSystem.Path.GetFullPath(filePath);
        var fullDirectory = fileSystem.Path.GetFullPath(directory);

        // The relative path is the bare file name only for a file directly inside the directory; ".." segments,
        // subdirectories and paths on another drive all yield something else.
        return fileSystem.Path.GetRelativePath(fullDirectory, fullPath) == fileSystem.Path.GetFileName(fullPath);
    }

    /// <summary>
    /// Deletes the files directly inside <paramref name="directory"/> that were last written longer than <paramref name="maxAge"/> ago.
    /// A missing directory is ignored. <paramref name="onDeleting"/> gets each file's path before it is deleted; I/O and access errors
    /// are passed to <paramref name="onFailure"/> with the directory or file path and don't stop the cleanup.
    /// </summary>
    public static void DeleteFilesOlderThan(
        this IFileSystem fileSystem,
        string directory,
        TimeSpan maxAge,
        Action<string> onDeleting,
        Action<Exception, string> onFailure)
    {
        if (!fileSystem.Directory.Exists(directory))
            return;

        var threshold = DateTime.UtcNow - maxAge;
        string[] files;

        try
        {
            files = fileSystem.Directory.GetFiles(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            onFailure(ex, directory);
            return;
        }

        foreach (var file in files)
        {
            try
            {
                if (fileSystem.File.GetLastWriteTimeUtc(file) < threshold)
                {
                    onDeleting(file);
                    fileSystem.File.Delete(file);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                onFailure(ex, file);
            }
        }
    }
}
