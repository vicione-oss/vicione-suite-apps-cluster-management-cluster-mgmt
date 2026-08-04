using System.IO.Abstractions;
using Sdk.Backend.IO;

namespace ClusterManagement.Backend.Tests;

/// <summary>
/// Test implementation of <see cref="IAtomicFileWriter"/> writing through the (mocked) file system:
/// the content is written to a temporary sibling file first and then moved into place.
/// </summary>
internal sealed class TestAtomicFileWriter(IFileSystem fileSystem) : IAtomicFileWriter
{
    public async Task WriteAsync(string filePath, Func<Stream, Task> writeContent, CancellationToken cancellationToken = default)
    {
        var directory = fileSystem.Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            fileSystem.Directory.CreateDirectory(directory);

        var temporaryFilePath = $"{filePath}.{Guid.NewGuid():N}.tmp";

        await using (var stream = fileSystem.FileStream.New(temporaryFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await writeContent(stream);
        }

        fileSystem.File.Move(temporaryFilePath, filePath, overwrite: true);
    }
}
