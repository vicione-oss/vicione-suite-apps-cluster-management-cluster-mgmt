using System.Buffers.Binary;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using ClusterManagement.Backend.Archives;
using ClusterManagement.Internal;

namespace ClusterManagement.Backend.Tests.Archives;

public class ArchiveExtractorTests
{
    private const uint LocalFileHeaderSignature = 0x04034b50;
    private const uint CentralDirectoryHeaderSignature = 0x02014b50;

    private static readonly string s_destination = MockUnixSupport.Path(@"c:\destination");

    private readonly MockFileSystem _fileSystem = new();

    private static ZipArchive OpenArchive(byte[] bytes)
        => new(new MemoryStream(bytes), ZipArchiveMode.Read);

    private static byte[] CreateArchiveBytes(params (string Name, byte[] Content)[] entries)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var (name, content) in entries)
            {
                using var entryStream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
                entryStream.Write(content);
            }
        }
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Overwrites the uncompressed size in every local and central directory header, like a zip bomb that understates its size.
    /// </summary>
    private static void ForgeUncompressedSizes(byte[] bytes, uint size)
    {
        for (var i = 0; i + 4 <= bytes.Length; i++)
        {
            var signature = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i));

            if (signature == LocalFileHeaderSignature)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i + 22), size);
            else if (signature == CentralDirectoryHeaderSignature)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i + 24), size);
        }
    }

    public sealed class Create : ArchiveExtractorTests
    {
        [Fact]
        public void Should_throw_if_archive_has_more_entries_than_allowed()
        {
            // Arrange
            using var archive = OpenArchive(CreateArchiveBytes(("a.txt", [1]), ("b.txt", [2])));
            var options = new ClusterManagementOptions { MaxArchiveEntries = 1 };

            // Act
            var act = () => ArchiveExtractor.Create(archive, _fileSystem, options);

            // Assert
            act.Should().Throw<ArchiveTooLargeException>();
        }

        [Fact]
        public void Should_throw_if_declared_sizes_exceed_limit()
        {
            // Arrange
            using var archive = OpenArchive(CreateArchiveBytes(("a.txt", new byte[60]), ("b.txt", new byte[60])));
            var options = new ClusterManagementOptions { MaxArchiveExtractedSize = 100 };

            // Act
            var act = () => ArchiveExtractor.Create(archive, _fileSystem, options);

            // Assert
            act.Should().Throw<ArchiveTooLargeException>();
        }

        [Fact]
        public void Should_throw_if_declared_size_is_huge()
        {
            // Arrange
            var bytes = CreateArchiveBytes(("a.txt", [1]), ("b.txt", [2]));
            ForgeUncompressedSizes(bytes, uint.MaxValue);
            using var archive = OpenArchive(bytes);

            // Act
            var act = () => ArchiveExtractor.Create(archive, _fileSystem, new ClusterManagementOptions());

            // Assert
            act.Should().Throw<ArchiveTooLargeException>();
        }

        [Fact]
        public void Should_accept_archive_at_the_limits()
        {
            // Arrange
            using var archive = OpenArchive(CreateArchiveBytes(("a.txt", new byte[50]), ("b.txt", new byte[50])));
            var options = new ClusterManagementOptions { MaxArchiveEntries = 2, MaxArchiveExtractedSize = 100 };

            // Act
            var act = () => ArchiveExtractor.Create(archive, _fileSystem, options);

            // Assert
            act.Should().NotThrow();
        }
    }

    public sealed class ExtractToDirectory : ArchiveExtractorTests
    {
        [Fact]
        public void Should_extract_files_and_directories_keeping_their_relative_paths()
        {
            // Arrange
            using var archive = OpenArchive(CreateArchiveBytes(("a.txt", [1]), ("sub/", []), ("sub/dir/b.txt", [2, 3])));
            var extractor = ArchiveExtractor.Create(archive, _fileSystem, new ClusterManagementOptions());

            // Act
            extractor.ExtractToDirectory(s_destination);

            // Assert
            _fileSystem.File.ReadAllBytes(_fileSystem.Path.Combine(s_destination, "a.txt")).Should().Equal(1);
            _fileSystem.File.ReadAllBytes(_fileSystem.Path.Combine(s_destination, "sub", "dir", "b.txt")).Should().Equal(2, 3);
        }

        [Theory]
        [InlineData("../outside.txt")]
        [InlineData("sub/../../outside.txt")]
        [InlineData("../destination-sibling/outside.txt")]
        public void Should_throw_if_entry_would_be_extracted_outside_of_destination(string entryName)
        {
            // Arrange
            using var archive = OpenArchive(CreateArchiveBytes((entryName, [1])));
            var extractor = ArchiveExtractor.Create(archive, _fileSystem, new ClusterManagementOptions());

            // Act
            var act = () => extractor.ExtractToDirectory(s_destination);

            // Assert
            act.Should().Throw<InvalidDataException>();
            _fileSystem.AllFiles.Should().BeEmpty();
        }

        [Fact]
        public void Should_extract_no_more_than_the_declared_size_although_the_entry_holds_more()
        {
            // Arrange
            // This is why checking the declared sizes is enough. Should the runtime ever read past the declared size,
            // the extractor stops at the limit and this test fails with an ArchiveTooLargeException.
            var bytes = CreateArchiveBytes(("a.txt", new byte[1_000_000]));
            ForgeUncompressedSizes(bytes, 10);
            using var archive = OpenArchive(bytes);
            var extractor = ArchiveExtractor.Create(archive, _fileSystem, new ClusterManagementOptions { MaxArchiveExtractedSize = 100_000 });

            // Act
            extractor.ExtractToDirectory(s_destination);

            // Assert
            _fileSystem.FileInfo.New(_fileSystem.Path.Combine(s_destination, "a.txt")).Length.Should().Be(10);
        }
    }

    public sealed class ExtractToFile : ArchiveExtractorTests
    {
        [Fact]
        public void Should_count_bytes_across_entries()
        {
            // Arrange
            using var archive = OpenArchive(CreateArchiveBytes(("a.txt", new byte[60])));
            var extractor = ArchiveExtractor.Create(archive, _fileSystem, new ClusterManagementOptions { MaxArchiveExtractedSize = 100 });
            _fileSystem.AddDirectory(s_destination);
            extractor.ExtractToFile(archive.Entries[0], _fileSystem.Path.Combine(s_destination, "first.txt"));

            // Act
            var act = () => extractor.ExtractToFile(archive.Entries[0], _fileSystem.Path.Combine(s_destination, "second.txt"));

            // Assert
            act.Should().Throw<ArchiveTooLargeException>();
        }

        [Fact]
        public void Should_remove_the_partial_file_when_the_limit_is_hit()
        {
            // Arrange
            // Entries of an update mode archive declare no size, so the limit is only hit while extracting.
            using ZipArchive archive = new(new MemoryStream(), ZipArchiveMode.Update);
            using (var stream = archive.CreateEntry("a.txt").Open())
                stream.Write(new byte[1_000]);
            var extractor = ArchiveExtractor.Create(archive, _fileSystem, new ClusterManagementOptions { MaxArchiveExtractedSize = 100 });
            _fileSystem.AddDirectory(s_destination);
            var path = _fileSystem.Path.Combine(s_destination, "a.txt");

            // Act
            var act = () => extractor.ExtractToFile(archive.Entries[0], path);

            // Assert
            act.Should().Throw<ArchiveTooLargeException>();
            _fileSystem.File.Exists(path).Should().BeFalse();
        }

        [Fact]
        public void Should_not_overwrite_an_existing_file()
        {
            // Arrange
            using var archive = OpenArchive(CreateArchiveBytes(("a.txt", [1])));
            var extractor = ArchiveExtractor.Create(archive, _fileSystem, new ClusterManagementOptions());
            var path = _fileSystem.Path.Combine(s_destination, "a.txt");
            _fileSystem.AddFile(path, new MockFileData("existing"));

            // Act
            var act = () => extractor.ExtractToFile(archive.Entries[0], path);

            // Assert
            act.Should().Throw<IOException>();
            _fileSystem.File.ReadAllText(path).Should().Be("existing");
        }

        [Fact]
        public void Should_throw_if_entry_belongs_to_another_archive()
        {
            // Arrange
            using var checkedArchive = OpenArchive(CreateArchiveBytes(("a.txt", [1])));
            using var otherArchive = OpenArchive(CreateArchiveBytes(("b.txt", [2])));
            var extractor = ArchiveExtractor.Create(checkedArchive, _fileSystem, new ClusterManagementOptions());
            _fileSystem.AddDirectory(s_destination);
            var path = _fileSystem.Path.Combine(s_destination, "b.txt");

            // Act
            var act = () => extractor.ExtractToFile(otherArchive.Entries[0], path);

            // Assert
            act.Should().Throw<ArgumentException>();
            _fileSystem.File.Exists(path).Should().BeFalse();
        }
    }
}
