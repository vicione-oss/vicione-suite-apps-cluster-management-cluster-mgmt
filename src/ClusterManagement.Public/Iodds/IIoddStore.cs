using System.IO.Compression;

namespace ClusterManagement.Public.Iodds;

public interface IIoddStore
{
    string IoddDirectory { get; }
    IReadOnlyCollection<DeviceInfo> Devices { get; }
    bool AutoDownloadIodds { get; set; }

    bool TryAddDevice(IoddId id);

    /// <summary>
    /// Adds a device from an IODD archive to the store.
    /// </summary>
    bool TryAddDevice(ZipArchive archive, out IoddId ioddId);

    /// <summary>
    /// Adds devices from an archive that contain multiple IODDs to the store.
    /// The structure of the archive should be: <vendor id>/<device id>/<IODD files>
    /// </summary>
    bool TryAddDevices(ZipArchive archive);

    bool TryRemoveDevice(IoddId id);

    bool TryRemoveDevices(IEnumerable<IoddId> ids);

    void FillDeviceArchive(ZipArchive archive, IoddId id);

    void Refresh();
}
