namespace ClusterManagement.Public.Iodds;

/// <summary>
/// Describes the outcome of <see cref="IIoddStore.AddDevice(System.IO.Compression.ZipArchive, out IoddId?)"/>.
/// </summary>
public enum AddDeviceResult
{
    /// <summary>
    /// The device was added to the store.
    /// </summary>
    Added,

    /// <summary>
    /// The archive does not contain an IODD file.
    /// </summary>
    NoIoddFile,

    /// <summary>
    /// A device with the same vendor and device id already exists in the store.
    /// </summary>
    AlreadyExists,

    /// <summary>
    /// The IODD files could not be extracted to the store.
    /// </summary>
    ExtractionFailed,

    /// <summary>
    /// The archive has more entries or extracts to more bytes than the configured archive limits allow; nothing of the device was kept.
    /// </summary>
    ArchiveTooLarge,
}
