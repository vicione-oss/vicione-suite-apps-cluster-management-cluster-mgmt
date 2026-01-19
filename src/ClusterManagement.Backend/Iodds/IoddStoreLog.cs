using Microsoft.Extensions.Logging;

namespace ClusterManagement.Backend.Iodds;

internal static partial class IoddStoreLog
{
    [LoggerMessage(LogLevel.Debug, "'{VendorDirectoryName}' is not a valid vendor directory name.")]
    public static partial void InvalidVendorDirectoryName(this ILogger<IoddStore> logger, string vendorDirectoryName);

    [LoggerMessage(LogLevel.Debug, "'{DeviceDirectoryName}' is not a valid device directory name.")]
    public static partial void InvalidDeviceDirectoryName(this ILogger<IoddStore> logger, string deviceDirectoryName);

    [LoggerMessage(LogLevel.Information, "Could not find IODD file in '{VendorId}/{DeviceId}' directory.")]
    public static partial void IoddFileNotFound(this ILogger<IoddStore> logger, int vendorId, int deviceId);

    [LoggerMessage(LogLevel.Debug, "Device '{VendorId}/{DeviceId}' already exists in the store.")]
    public static partial void DeviceAlreadyExists(this ILogger<IoddStore> logger, int vendorId, int deviceId);

    [LoggerMessage(LogLevel.Warning, "Could not parse IODD file '{FilePath}'.")]
    public static partial void IoddFileParseError(this ILogger<IoddStore> logger, string filePath, Exception exception);

    [LoggerMessage(LogLevel.Information, "Added '{DeviceCount}' devices to the store in {ElapsedMilliseconds}ms.")]
    public static partial void AddedDevicesToStore(this ILogger<IoddStore> logger, int deviceCount, long elapsedMilliseconds);

    [LoggerMessage(LogLevel.Warning, "Could not extract device '{VendorId}/{DeviceId}' from archive.")]
    public static partial void DeviceExtractionError(this ILogger<IoddStore> logger, int vendorId, int deviceId, Exception exception);
}
