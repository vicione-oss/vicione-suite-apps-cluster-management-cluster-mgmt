using System.IO.Compression;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Requests;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public sealed class DownloadIoddConsumer(IIoddStore ioddStore) :
    RequestConsumer<DownloadIoddRequest, DownloadIoddResponse>
{
    protected override Task<DownloadIoddResponse> HandleException(ConsumeContext<DownloadIoddRequest> context, Exception e)
        => Task.FromResult(new DownloadIoddResponse() { RequestError = new ErrorInfo(400, "failed to download iodd") });

    protected override async Task<DownloadIoddResponse> Respond(ConsumeContext<DownloadIoddRequest> context)
    {
        var id = context.Message.Id;
        var device = ioddStore.Devices.FirstOrDefault(d => d.DeviceDescription.DeviceIdentity.DeviceId == id.DeviceId
                                                        && d.DeviceDescription.DeviceIdentity.VendorId == id.VendorId)
            ?? throw new ArgumentException($"Cannot find device '{id.DeviceId}' ({id.VendorId})");

        using MemoryStream stream = new();
        using (ZipArchive zipArchive = new(stream, ZipArchiveMode.Update, true))
            ioddStore.FillDeviceArchive(zipArchive, id);

        await stream.FlushAsync();

        stream.Position = 0;
        var bytes = stream.ToArray();

        return new() { Content = bytes, Filename = device.Filename, };
    }
}
