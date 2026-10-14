using System.IO.Compression;

namespace ClusterManagement.Public.Iodds;

/// <summary>
/// Represents a store that manages IODD (IO Device Description) files and associated device metadata.
/// </summary>
public interface IIoddStore
{
    /// <summary>
    /// Gets the directory path where IODD files are stored locally.
    /// </summary>
    string IoddDirectory { get; }

    /// <summary>
    /// Gets the collection of devices currently available in the store.
    /// </summary>
    IReadOnlyCollection<DeviceInfo> Devices { get; }

    /// <summary>
    /// Gets a value indicating whether IODD files should be automatically downloaded
    /// when missing or required.
    /// </summary>
    bool GetAutoDownloadIodds();

    /// <summary>
    /// Sets a value indicating whether IODD files should be automatically downloaded
    /// when missing or required.
    /// </summary>
    Task SetAutoDownloadIodds(bool value, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to add a device to the store using its IODD ID.
    /// </summary>
    /// <param name="id">The identifier of the IODD device.</param>
    bool TryAddDevice(IoddId id);

    /// <summary>
    /// Adds a device from a single-device IODD archive.
    /// </summary>
    /// <param name="archive">The ZIP archive containing the IODD files.</param>
    /// <param name="ioddId">
    /// When this method returns, contains the IODD ID read from the archive if the result is
    /// <see cref="AddDeviceResult.Added"/>, <see cref="AddDeviceResult.AlreadyExists"/> or <see cref="AddDeviceResult.ExtractionFailed"/>.
    /// </param>
    /// <returns>Whether the device was added, or why not.</returns>
    AddDeviceResult AddDevice(ZipArchive archive, out IoddId? ioddId);

    /// <summary>
    /// Attempts to add multiple devices from an archive that contains IODDs in a structured format.
    /// </summary>
    /// <param name="archive">
    /// A ZIP archive with the expected folder structure: 
    /// <c>&lt;vendor-id&gt;/&lt;device-id&gt;/&lt;IODD files&gt;</c>.
    /// </param>
    bool TryAddDevices(ZipArchive archive);

    /// <summary>
    /// Attempts to remove a device with the specified IODD ID from the store.
    /// </summary>
    /// <param name="id">The IODD ID of the device to remove.</param>
    bool TryRemoveDevice(IoddId id);

    /// <summary>
    /// Attempts to remove multiple devices from the store using their IODD IDs.
    /// </summary>
    /// <param name="ids">The collection of IODD IDs representing the devices to remove.</param>
    bool TryRemoveDevices(IEnumerable<IoddId> ids);

    /// <summary>
    /// Adds all IODD files of the specified devices of a single vendor to the provided ZIP archive.
    /// </summary>
    /// <param name="archive">The target archive to fill.</param>
    /// <param name="vendorId">The vendor whose devices will be archived.</param>
    /// <param name="deviceIds">The devices of the vendor to archive.</param>
    /// <returns>The device IDs that were actually added to the archive.</returns>
    /// <remarks>
    /// The entries are created with the structure <c>&lt;vendor-id&gt;/&lt;device-id&gt;/&lt;IODD files&gt;</c>,
    /// so the resulting archive can be imported again via <see cref="TryAddDevices(ZipArchive)"/>.
    /// Devices without a readable directory are skipped.
    /// </remarks>
    IReadOnlyCollection<int> FillVendorArchive(ZipArchive archive, int vendorId, IEnumerable<int> deviceIds);

    /// <summary>
    /// Reloads the device list and reinitializes the store from its backing storage.
    /// </summary>
    void Refresh();
}
