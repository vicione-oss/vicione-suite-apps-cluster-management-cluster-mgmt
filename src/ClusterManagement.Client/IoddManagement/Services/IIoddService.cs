using ClusterManagement.Public.Iodds;

namespace ClusterManagement.Client.IoddManagement.Services;

public interface IIoddService
{
    Task DeleteIodd(int vendorId, int deviceId);
    Task<(byte[], string)> DownloadIodd(int vendorId, int deviceId);
    Task<IEnumerable<DeviceInfo>> GetIODevices();
    Task<string> GetIODeviceImage(int vendorId, int deviceId, string imageFilename);
    Task<bool> GetIsAutoDownloading();
    Task SetIsAutoDownloading(bool value);
    Task UploadIodd(byte[] data);
}
