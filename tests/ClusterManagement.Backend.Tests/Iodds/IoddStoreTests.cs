using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using ClusterManagement.Backend.Iodds;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sdk.Backend.Modules;
using Xunit;

namespace ClusterManagement.Backend.Tests.Iodds;

public class IoddStore_TryAddDevice_Archive
{
    [Fact]
    public void Acceptance()
    {
        MockFileSystem fileSystem = new();
        IoddStore ioddStore = new(new NullLogger<IoddStore>(),
                                  Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>(),
                                  fileSystem,
                                  Substitute.For<IServiceProvider>());

        using ZipArchive archive = new(new MemoryStream(), ZipArchiveMode.Update);
        var ioddFileEntry = archive.CreateEntry("ifm-myDevice-20250323-IODD1.1.xml");
        using (var stream = ioddFileEntry.Open())
        {
            using StreamWriter writer = new(stream);
            writer.Write(IoddConstants.IoddDeviceTwo);
        }

        ioddStore.TryAddDevice(archive, out _).Should().BeTrue();

        ioddStore.Devices.Should().ContainSingle();
    }
}

public class IoddStore_TryRemoveDevice
{
    [Fact]
    public void Acceptance()
    {
        MockFileSystem fileSystem = new();
        IoddStore ioddStore = new(new NullLogger<IoddStore>(),
                                  Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>(),
                                  fileSystem,
                                  Substitute.For<IServiceProvider>());

        fileSystem.AddDirectory(ioddStore.IoddDirectory);
        fileSystem.AddDirectory(fileSystem.Path.Combine(ioddStore.IoddDirectory, "310"));
        fileSystem.AddDirectory(fileSystem.Path.Combine(ioddStore.IoddDirectory, "310", "2"));
        fileSystem.AddFile(fileSystem.Path.Combine(ioddStore.IoddDirectory, "310", "2", "ifm-myDevice-20250323-IODD1.0.1.xml"), IoddConstants.IoddDeviceTwo);
        ioddStore.Refresh();
        var device = ioddStore.Devices.Should().ContainSingle().Which;

        ioddStore.TryRemoveDevice(new(device.DeviceDescription.DeviceIdentity.VendorId, device.DeviceDescription.DeviceIdentity.DeviceId)).Should().BeTrue();
        fileSystem.Directory.GetDirectories(ioddStore.IoddDirectory).Should().BeEmpty();
    }
}

public class IoddStore_TryAddDevices
{
    [Fact]
    public void Acceptance()
    {
        MockFileSystem fileSystem = new();
        IoddStore ioddStore = new(new NullLogger<IoddStore>(),
                                  Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>(),
                                  fileSystem,
                                  Substitute.For<IServiceProvider>());

        using ZipArchive archive = new(new MemoryStream(), ZipArchiveMode.Update);
        var ioddFileEntryOne = archive.CreateEntry("310/2/ifm-myDevice-20250323-IODD1.1.xml");
        using (var stream = ioddFileEntryOne.Open())
        {
            using StreamWriter writer = new(stream);
            writer.Write(IoddConstants.IoddDeviceTwo);
        }
        var ioddFileEntryTwo = archive.CreateEntry("311/3/ifm-myDevice-20250323-IODD1.1.xml");
        using (var stream = ioddFileEntryTwo.Open())
        {
            using StreamWriter writer = new(stream);
            writer.Write(IoddConstants.IoddDeviceThree);
        }

        ioddStore.TryAddDevices(archive).Should().BeTrue();

        ioddStore.Devices.OrderBy(e => e.DeviceDescription.DeviceIdentity.VendorId).Should().HaveCount(2).And.SatisfyRespectively(
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

public class IoddStore_FillDeviceArchive
{
    [Fact]
    public void Acceptance()
    {
        MockFileSystem fileSystem = new();
        IoddStore ioddStore = new(new NullLogger<IoddStore>(),
                                  Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>(),
                                  fileSystem,
                                  Substitute.For<IServiceProvider>());

        fileSystem.AddDirectory(ioddStore.IoddDirectory);
        fileSystem.AddDirectory(fileSystem.Path.Combine(ioddStore.IoddDirectory, "310"));
        fileSystem.AddDirectory(fileSystem.Path.Combine(ioddStore.IoddDirectory, "310", "2"));
        fileSystem.AddFile(fileSystem.Path.Combine(ioddStore.IoddDirectory, "310", "2", "ifm-myDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceTwo);
        ioddStore.Refresh();
        var device = ioddStore.Devices.Should().ContainSingle().Which;

        MemoryStream stream = new();
        using ZipArchive archive = new(stream, ZipArchiveMode.Update, true);
        ioddStore.FillDeviceArchive(archive, new(device.DeviceDescription.DeviceIdentity.VendorId, device.DeviceDescription.DeviceIdentity.DeviceId));

        using var fileStream = archive.GetEntry("ifm-myDevice-20250323-IODD1.1.xml")!.Open();
        using StreamReader reader = new(fileStream);
        var content = reader.ReadToEnd();
        content.Should().Be(IoddConstants.IoddDeviceTwo);
    }
}

public class IoddStore_Refresh
{
    [Fact]
    public void Acceptance()
    {
        MockFileSystem fileSystem = new();
        IoddStore ioddStore = new(new NullLogger<IoddStore>(),
                                  Substitute.For<IWorkspaceProvider<ClusterManagementBackendModule>>(),
                                  fileSystem,
                                  Substitute.For<IServiceProvider>());

        fileSystem.AddDirectory(ioddStore.IoddDirectory);
        fileSystem.AddDirectory(fileSystem.Path.Combine(ioddStore.IoddDirectory, "310"));
        fileSystem.AddDirectory(fileSystem.Path.Combine(ioddStore.IoddDirectory, "310", "2"));
        fileSystem.AddFile(fileSystem.Path.Combine(ioddStore.IoddDirectory, "310", "2", "ifm-myDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceTwo);

        ioddStore.Refresh();
        ioddStore.Devices.Should().ContainSingle();

        fileSystem.AddDirectory(fileSystem.Path.Combine(ioddStore.IoddDirectory, "311"));
        fileSystem.AddDirectory(fileSystem.Path.Combine(ioddStore.IoddDirectory, "311", "3"));
        fileSystem.AddFile(fileSystem.Path.Combine(ioddStore.IoddDirectory, "311", "3", "ifm-myDevice-20250323-IODD1.1.xml"), IoddConstants.IoddDeviceThree);
        fileSystem.Directory.Delete(fileSystem.Path.Combine(ioddStore.IoddDirectory, "310", "2"), true);

        ioddStore.Refresh();
        ioddStore.Devices.Should().ContainSingle().Which.DeviceDescription.DeviceIdentity.DeviceId.Should().Be(3);
    }
}
