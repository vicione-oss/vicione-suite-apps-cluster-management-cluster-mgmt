using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using ClusterManagement.Backend.Iodds;
using ClusterManagement.Public.Iodds;
using Microsoft.Extensions.Logging.Abstractions;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Tests.Iodds;

public class IoddStoreTests
{
    private readonly MockFileSystem _fileSystem = new();

    private IoddStore CreateStore() => new(
        new NullLogger<IoddStore>(),
        Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>(),
        _fileSystem,
        Substitute.For<IServiceProvider>());

    public sealed class AddDevice : IoddStoreTests
    {
        [Fact]
        public void Should_add_device_from_single_device_archive()
        {
            // Arrange
            var store = CreateStore();
            using ZipArchive archive = new(new MemoryStream(), ZipArchiveMode.Update);
            var ioddEntry = archive.CreateEntry("ifm-myDevice-20250323-IODD1.1.xml");
            using (var stream = ioddEntry.Open())
            {
                using StreamWriter writer = new(stream);
                writer.Write(IoddConstants.IoddDeviceTwo);
            }

            // Act
            var result = store.AddDevice(archive, out _);

            // Assert
            result.Should().Be(AddDeviceResult.Added);
            store.Devices.Should().ContainSingle();
        }

        [Fact]
        public void Should_return_no_iodd_file_when_archive_has_no_iodd_entry()
        {
            // Arrange
            var store = CreateStore();
            using ZipArchive archive = new(new MemoryStream(), ZipArchiveMode.Update);
            archive.CreateEntry("readme.txt");

            // Act
            var result = store.AddDevice(archive, out var ioddId);

            // Assert
            result.Should().Be(AddDeviceResult.NoIoddFile);
            ioddId.Should().BeNull();
        }

        [Fact]
        public void Should_set_iodd_id_on_success()
        {
            // Arrange
            var store = CreateStore();
            using ZipArchive archive = new(new MemoryStream(), ZipArchiveMode.Update);
            var ioddEntry = archive.CreateEntry("ifm-myDevice-20250323-IODD1.1.xml");
            using (var stream = ioddEntry.Open())
            {
                using StreamWriter writer = new(stream);
                writer.Write(IoddConstants.IoddDeviceTwo);
            }

            // Act
            store.AddDevice(archive, out var ioddId);

            // Assert
            ioddId.Should().NotBeNull();
            ioddId!.VendorId.Should().Be(310);
            ioddId!.DeviceId.Should().Be(2);
        }

        [Fact]
        public void Should_return_already_exists_with_iodd_id_when_device_already_exists()
        {
            // Arrange
            var store = CreateStore();
            using ZipArchive archive = new(new MemoryStream(), ZipArchiveMode.Update);
            var ioddEntry = archive.CreateEntry("ifm-myDevice-20250323-IODD1.1.xml");
            using (var stream = ioddEntry.Open())
            {
                using StreamWriter writer = new(stream);
                writer.Write(IoddConstants.IoddDeviceTwo);
            }
            store.AddDevice(archive, out _);

            // Act
            var result = store.AddDevice(archive, out var ioddId);

            // Assert
            result.Should().Be(AddDeviceResult.AlreadyExists);
            ioddId.Should().Be(new IoddId(310, 2));
            store.Devices.Should().ContainSingle();
        }
    }

    public sealed class TryAddDevice : IoddStoreTests
    {
        [Fact]
        public void Should_add_device_from_disk()
        {
            // Arrange
            var store = CreateStore();
            _fileSystem.AddDirectory(store.IoddDirectory);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310"));
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2", "ifm-myDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceTwo);

            // Act
            var result = store.TryAddDevice(new(310, 2));

            // Assert
            result.Should().BeTrue();
            store.Devices.Should().ContainSingle();
        }

        [Fact]
        public void Should_return_false_when_no_iodd_file_on_disk()
        {
            // Arrange
            var store = CreateStore();
            _fileSystem.AddDirectory(store.IoddDirectory);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310"));
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2"));

            // Act
            var result = store.TryAddDevice(new(310, 2));

            // Assert
            result.Should().BeFalse();
            store.Devices.Should().BeEmpty();
        }

        [Fact]
        public void Should_return_false_when_device_already_in_store()
        {
            // Arrange
            var store = CreateStore();
            _fileSystem.AddDirectory(store.IoddDirectory);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310"));
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2", "ifm-myDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceTwo);
            store.TryAddDevice(new(310, 2));

            // Act
            var result = store.TryAddDevice(new(310, 2));

            // Assert
            result.Should().BeFalse();
            store.Devices.Should().ContainSingle();
        }
    }

    public sealed class TryRemoveDevice : IoddStoreTests
    {
        [Fact]
        public void Should_remove_device_and_clean_up_directories()
        {
            // Arrange
            var store = CreateStore();
            _fileSystem.AddDirectory(store.IoddDirectory);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310"));
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2", "ifm-myDevice-20250323-IODD1.0.1.xml"), IoddConstants.IoddDeviceTwo);
            store.Refresh();
            var device = store.Devices.Should().ContainSingle().Which;

            // Act
            var result = store.TryRemoveDevice(new(device.DeviceDescription.DeviceIdentity.VendorId, device.DeviceDescription.DeviceIdentity.DeviceId));

            // Assert
            result.Should().BeTrue();
            _fileSystem.Directory.GetDirectories(store.IoddDirectory).Should().BeEmpty();
        }

        [Fact]
        public void Should_return_false_when_device_not_in_store()
        {
            // Arrange
            var store = CreateStore();

            // Act
            var result = store.TryRemoveDevice(new(310, 2));

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void Should_preserve_vendor_directory_when_sibling_device_exists()
        {
            // Arrange
            var store = CreateStore();
            _fileSystem.AddDirectory(store.IoddDirectory);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310"));
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2", "ifm-myDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceTwo);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310", "1500"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(store.IoddDirectory, "310", "1500", "ifm-myOtherDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceRevOneOne);
            store.Refresh();
            store.Devices.Should().HaveCount(2);

            // Act
            store.TryRemoveDevice(new(310, 2));

            // Assert
            _fileSystem.Directory.Exists(_fileSystem.Path.Combine(store.IoddDirectory, "310")).Should().BeTrue();
            store.Devices.Should().ContainSingle().Which.DeviceDescription.DeviceIdentity.DeviceId.Should().Be(1500);
        }
    }

    public sealed class TryRemoveDevices : IoddStoreTests
    {
        [Fact]
        public void Should_remove_all_specified_devices()
        {
            // Arrange
            var store = CreateStore();
            _fileSystem.AddDirectory(store.IoddDirectory);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310"));
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2", "ifm-myDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceTwo);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "311"));
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "311", "3"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(store.IoddDirectory, "311", "3", "ifm-myDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceThree);
            store.Refresh();
            store.Devices.Should().HaveCount(2);

            // Act
            var result = store.TryRemoveDevices([new(310, 2), new(311, 3)]);

            // Assert
            result.Should().BeTrue();
            store.Devices.Should().BeEmpty();
            _fileSystem.Directory.GetDirectories(store.IoddDirectory).Should().BeEmpty();
        }

        [Fact]
        public void Should_return_false_when_any_device_not_found()
        {
            // Arrange
            var store = CreateStore();
            _fileSystem.AddDirectory(store.IoddDirectory);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310"));
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2", "ifm-myDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceTwo);
            store.Refresh();
            store.Devices.Should().ContainSingle();

            // Act
            var result = store.TryRemoveDevices([new(310, 2), new(999, 999)]);

            // Assert
            result.Should().BeFalse();
            store.Devices.Should().BeEmpty();
        }
    }

    public sealed class TryAddDevices : IoddStoreTests
    {
        [Fact]
        public void Should_add_all_devices_from_multi_device_archive()
        {
            // Arrange
            var store = CreateStore();
            using ZipArchive archive = new(new MemoryStream(), ZipArchiveMode.Update);
            var ioddEntryOne = archive.CreateEntry("310/2/ifm-myDevice-20250323-IODD1.1.xml");
            using (var stream = ioddEntryOne.Open())
            {
                using StreamWriter writer = new(stream);
                writer.Write(IoddConstants.IoddDeviceTwo);
            }
            var ioddEntryTwo = archive.CreateEntry("311/3/ifm-myDevice-20250323-IODD1.1.xml");
            using (var stream = ioddEntryTwo.Open())
            {
                using StreamWriter writer = new(stream);
                writer.Write(IoddConstants.IoddDeviceThree);
            }

            // Act
            var result = store.TryAddDevices(archive);

            // Assert
            result.Should().BeTrue();
            store.Devices.OrderBy(e => e.DeviceDescription.DeviceIdentity.VendorId).Should().HaveCount(2).And.SatisfyRespectively(
                first =>
                {
                    first.DeviceDescription.DeviceIdentity.VendorId.Should().Be(310);
                    first.DeviceDescription.DeviceIdentity.DeviceId.Should().Be(2);
                },
                second =>
                {
                    second.DeviceDescription.DeviceIdentity.VendorId.Should().Be(311);
                    second.DeviceDescription.DeviceIdentity.DeviceId.Should().Be(3);
                });
        }
    }

    public sealed class FillVendorArchive : IoddStoreTests
    {
        [Fact]
        public void Should_populate_archive_with_vendor_and_device_folders()
        {
            // Arrange
            var store = CreateStore();
            _fileSystem.AddDirectory(store.IoddDirectory);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310"));
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2", "ifm-myDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceTwo);
            store.Refresh();
            var device = store.Devices.Should().ContainSingle().Which;
            using var memoryStream = new MemoryStream();
            using ZipArchive archive = new(memoryStream, ZipArchiveMode.Update, true);

            // Act
            var archived = store.FillVendorArchive(archive, device.DeviceDescription.DeviceIdentity.VendorId, [device.DeviceDescription.DeviceIdentity.DeviceId]);

            // Assert
            archived.Should().BeEquivalentTo([2]);
            using var fileStream = archive.GetEntry("310/2/ifm-myDevice-20250323-IODD1.1.xml")!.Open();
            using StreamReader reader = new(fileStream);
            reader.ReadToEnd().Should().Be(IoddConstants.IoddDeviceTwo);
        }

        [Fact]
        public void Should_skip_devices_without_directory()
        {
            // Arrange
            var store = CreateStore();
            _fileSystem.AddDirectory(store.IoddDirectory);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310"));
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2", "ifm-myDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceTwo);
            using var memoryStream = new MemoryStream();
            using ZipArchive archive = new(memoryStream, ZipArchiveMode.Update, true);

            // Act
            var archived = store.FillVendorArchive(archive, 310, [2, 99]);

            // Assert
            archived.Should().BeEquivalentTo([2]);
            archive.Entries.Should().ContainSingle().Which.FullName.Should().Be("310/2/ifm-myDevice-20250323-IODD1.1.xml");
        }

        [Fact]
        public void Should_throw_when_vendor_directory_not_found()
        {
            // Arrange
            var store = CreateStore();
            using var memoryStream = new MemoryStream();
            using ZipArchive archive = new(memoryStream, ZipArchiveMode.Update, true);

            // Act
            var act = () => store.FillVendorArchive(archive, 310, [2]);

            // Assert
            act.Should().Throw<DirectoryNotFoundException>();
        }
    }

    public sealed class Refresh : IoddStoreTests
    {
        [Fact]
        public void Should_reflect_file_system_changes_on_subsequent_calls()
        {
            // Arrange
            var store = CreateStore();
            _fileSystem.AddDirectory(store.IoddDirectory);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310"));
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2", "ifm-myDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceTwo);
            store.Refresh();
            store.Devices.Should().ContainSingle();

            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "311"));
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "311", "3"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(store.IoddDirectory, "311", "3", "ifm-myDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceThree);
            _fileSystem.Directory.Delete(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2"), true);

            // Act
            store.Refresh();

            // Assert
            store.Devices.Should().ContainSingle().Which.DeviceDescription.DeviceIdentity.DeviceId.Should().Be(3);
        }

        [Fact]
        public void Should_skip_vendor_directories_with_non_numeric_names()
        {
            // Arrange
            var store = CreateStore();
            _fileSystem.AddDirectory(store.IoddDirectory);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310"));
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2", "ifm-myDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceTwo);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "invalid-vendor"));

            // Act
            store.Refresh();

            // Assert
            store.Devices.Should().ContainSingle();
        }

        [Fact]
        public void Should_skip_device_directories_with_non_numeric_names()
        {
            // Arrange
            var store = CreateStore();
            _fileSystem.AddDirectory(store.IoddDirectory);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310"));
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2", "ifm-myDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceTwo);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310", "invalid-device"));

            // Act
            store.Refresh();

            // Assert
            store.Devices.Should().ContainSingle();
        }

        [Fact]
        public void Should_be_idempotent_for_existing_devices()
        {
            // Arrange
            var store = CreateStore();
            _fileSystem.AddDirectory(store.IoddDirectory);
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310"));
            _fileSystem.AddDirectory(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2"));
            _fileSystem.AddFile(_fileSystem.Path.Combine(store.IoddDirectory, "310", "2", "ifm-myDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceTwo);
            store.Refresh();
            store.Devices.Should().ContainSingle();

            // Act
            store.Refresh();

            // Assert
            store.Devices.Should().ContainSingle();
        }
    }
}
