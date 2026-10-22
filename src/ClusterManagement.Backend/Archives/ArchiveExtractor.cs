using System.Buffers;
using System.IO.Abstractions;
using System.IO.Compression;
using ClusterManagement.Internal;

namespace ClusterManagement.Backend.Archives;

/// <summary>
/// Extracts an uploaded archive within <see cref="ClusterManagementOptions.MaxArchiveEntries"/> and
/// <see cref="ClusterManagementOptions.MaxArchiveExtractedSize"/>, so a small, highly compressed archive (a zip bomb)
/// cannot fill the disk. Packages and IODDs share these limits.
/// </summary>
/// <remarks>
/// The limits are checked against the sizes the archive declares before anything is extracted. That is enough for an
/// archive opened in <see cref="ZipArchiveMode.Read"/>, because its entry streams end at the declared size even if the
/// compressed data holds more. The bytes are still counted while extracting, which covers archives opened in another mode,
/// whose entries declare no size.
/// </remarks>
internal sealed class ArchiveExtractor
{
    private const int BufferSize = 81920;

    private readonly ZipArchive _archive;
    private readonly IFileSystem _fileSystem;
    private readonly long _maxExtractedSize;
    private long _extractedSize;

    private ArchiveExtractor(ZipArchive archive, IFileSystem fileSystem, long maxExtractedSize)
    {
        _archive = archive;
        _fileSystem = fileSystem;
        _maxExtractedSize = maxExtractedSize;
    }

    /// <summary>
    /// Checks the entry count and the sizes declared by <paramref name="archive"/> before anything is extracted.
    /// </summary>
    /// <exception cref="ArchiveTooLargeException">The archive exceeds one of the limits.</exception>
    public static ArchiveExtractor Create(ZipArchive archive, IFileSystem fileSystem, ClusterManagementOptions options)
    {
        if (archive.Entries.Count > options.MaxArchiveEntries)
            throw new ArchiveTooLargeException($"The archive contains {archive.Entries.Count} entries, more than the {options.MaxArchiveEntries} allowed.");

        if (archive.Mode == ZipArchiveMode.Read)
            CheckDeclaredSizes(archive, options.MaxArchiveExtractedSize);

        return new(archive, fileSystem, options.MaxArchiveExtractedSize);
    }

    private static void CheckDeclaredSizes(ZipArchive archive, long maxExtractedSize)
    {
        // Subtracting instead of summing can't overflow, whatever sizes the headers declare.
        var remainingSize = maxExtractedSize;

        foreach (var entry in archive.Entries)
        {
            if (entry.Length > remainingSize)
                throw new ArchiveTooLargeException($"The archive extracts to more than the {maxExtractedSize} bytes allowed.");

            remainingSize -= entry.Length;
        }
    }

    /// <summary>
    /// Extracts all entries of the archive below <paramref name="destinationDirectory"/>, keeping their relative paths.
    /// Fails if a file already exists.
    /// </summary>
    /// <exception cref="InvalidDataException">An entry would be extracted outside of <paramref name="destinationDirectory"/>.</exception>
    /// <exception cref="ArchiveTooLargeException">The extracted bytes exceed the limit.</exception>
    public void ExtractToDirectory(string destinationDirectory)
    {
        var fullDestinationDirectory = _fileSystem.Path.GetFullPath(destinationDirectory);

        // The separator keeps a sibling like "<destination>-other" from passing the check below.
        var destinationPrefix = _fileSystem.Path.EndsInDirectorySeparator(fullDestinationDirectory)
            ? fullDestinationDirectory
            : fullDestinationDirectory + _fileSystem.Path.DirectorySeparatorChar;

        foreach (var entry in _archive.Entries)
        {
            var path = _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(fullDestinationDirectory, entry.FullName));

            if (!path.StartsWith(destinationPrefix, StringComparison.Ordinal))
                throw new InvalidDataException($"The entry '{entry.FullName}' would be extracted outside of the destination directory.");

            // Directory entries have no name, only a full name ending with a separator.
            if (entry.Name.Length == 0)
            {
                _fileSystem.Directory.CreateDirectory(path);
                continue;
            }

            _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(path)!);
            ExtractToFile(entry, path);
        }
    }

    /// <summary>
    /// Extracts <paramref name="entry"/> of the archive to the new file <paramref name="destinationPath"/>.
    /// The size limit is shared by all entries extracted with this instance.
    /// </summary>
    /// <remarks>
    /// If extracting fails, the file created for it is removed again. A file that already existed is never touched.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="entry"/> belongs to another archive.</exception>
    /// <exception cref="ArchiveTooLargeException">The extracted bytes exceed the limit.</exception>
    public void ExtractToFile(ZipArchiveEntry entry, string destinationPath)
    {
        if (entry.Archive != _archive)
            throw new ArgumentException("The entry belongs to another archive than the one that was checked.", nameof(entry));

        // Fails without creating anything if the file exists, so the cleanup below only removes our own file.
        var destination = _fileSystem.File.Open(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        try
        {
            using (destination)
                CopyWithinLimit(entry, destination);
        }
        catch
        {
            TryDeleteFile(destinationPath);
            throw;
        }
    }

    private void CopyWithinLimit(ZipArchiveEntry entry, Stream destination)
    {
        using var source = entry.Open();
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                _extractedSize += read;

                if (_extractedSize > _maxExtractedSize)
                    throw new ArchiveTooLargeException($"The archive extracts to more than the {_maxExtractedSize} bytes allowed.");

                destination.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            _fileSystem.File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Keep the original exception; whoever owns the destination directory removes the leftover.
        }
    }
}
