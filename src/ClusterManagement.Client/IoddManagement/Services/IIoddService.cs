using ClusterManagement.Client.IoddManagement.Models;
using ClusterManagement.Public.Iodds.Events;

namespace ClusterManagement.Client.IoddManagement.Services;

/// <summary>
/// Provides operations for managing IODDs (IO Device Descriptions), including
/// uploading, downloading, deleting, and querying IO devices and their images.
/// </summary>
public interface IIoddService
{
    /// <summary>
    /// Raised when an IODD has changed (e.g. added or deleted).
    /// </summary>
    event Func<IoddChanged, CancellationToken, Task>? IoddChanged;

    /// <summary>
    /// Raised when the automatic IODD download setting has changed.
    /// </summary>
    event Func<AutoIoddDownloadChanged, CancellationToken, Task>? AutoIoddDownloadChanged;

    /// <summary>
    /// Sends a delete command for the specified IODD and waits for a correlating
    /// <see cref="IoddChanged"/> event or a timeout.
    /// </summary>    
    Task<IIoddServiceResult> DeleteIodd(int vendorId, int deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the IODD file for the specified device.
    /// </summary>    
    Task<(byte[], string)> DownloadIodd(int vendorId, int deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all available IO devices.
    /// </summary>    
    Task<IEnumerable<DeviceView>> GetIODevices(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a device image as a Base64-encoded string.
    /// </summary>    
    Task<string> GetIODeviceImage(int vendorId, int deviceId, string imageFilename, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether automatic IODD downloading is currently enabled.
    /// </summary>    
    Task<bool> GetIsAutoDownloading(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command to enable or disable automatic IODD downloading and waits for a correlating
    /// <see cref="AutoIoddDownloadChanged"/> event or a timeout.
    /// </summary>    
    Task<IIoddServiceResult> SetIsAutoDownloading(bool value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an upload command for the provided IODD data and waits for a correlating
    /// <see cref="IoddChanged"/> event or a timeout.
    /// </summary>    
    Task<IIoddServiceResult> UploadIodd(byte[] data, CancellationToken cancellationToken = default);
}
