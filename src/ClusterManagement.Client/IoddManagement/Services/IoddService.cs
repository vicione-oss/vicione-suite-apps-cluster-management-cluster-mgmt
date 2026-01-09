using ClusterManagement.Internal.Commands;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Requests;
using Sdk.Client.Infrastructure;

namespace ClusterManagement.Client.IoddManagement.Services;

public sealed class IoddService(IUiMediator mediator) : IIoddService
{
    public async Task DeleteIodd(int vendorId, int deviceId)
        => await mediator.Send<DeleteIodd>(new(vendorId, deviceId));

    public async Task<(byte[], string)> DownloadIodd(int vendorId, int deviceId)
    {
        var response = await mediator.Request<DownloadIoddRequest, DownloadIoddResponse>(new DownloadIoddRequest() { Id = new IoddId(vendorId, deviceId), });
        return (response.Content, response.Filename);
    }

    public async Task<IEnumerable<DeviceInfo>> GetIODevices()
    {
        var response = await mediator.Request<GetIODevicesRequest, GetIODevicesResponse>(new GetIODevicesRequest());
        return response.IODevices;
    }

    public async Task<string> GetIODeviceImage(int vendorId, int deviceId, string imageFilename)
    {
        var response = await mediator.Request<GetIODeviceImageConsumerRequest, GetIODeviceImageResponse>(new()
        {
            DeviceId = deviceId,
            VendorId = vendorId,
            ImageFilename = imageFilename
        });

        if (response.RequestError is not null)
            return string.Empty;
        return response.ImageAsBase64;
    }

    public async Task UploadIodd(byte[] data)
        => await mediator.Send<AddIodd>(new(data));

    public async Task<bool> GetIsAutoDownloading()
    {
        var result = await mediator.Request<GetAutoIoddDownloadRequest, GetAutoIoddDownloadResponse>(new GetAutoIoddDownloadRequest());
        return result.IsAutoDownload;
    }

    public async Task SetIsAutoDownloading(bool value)
        => await mediator.Send(new SetAutoIoddDownload(value));
}
