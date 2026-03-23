using System.IO.Compression;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Requests;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public sealed class DownloadIoddConsumer(IIoddStore ioddStore) :
    RequestConsumer<DownloadIoddRequest, DownloadIoddResponse>
{
    public override Task<DownloadIoddResponse> HandleException(DownloadIoddRequest context, Exception e, CancellationToken cancellationToken)
        => Task.FromResult(new DownloadIoddResponse() { RequestError = new ErrorInfo(400, "failed to download iodd") });

    public override async Task<DownloadIoddResponse> Respond(DownloadIoddRequest message, CancellationToken cancellationToken)
    {
        var id = message.Id;
        var device = ioddStore.Devices.FirstOrDefault(d => d.DeviceDescription.DeviceIdentity.DeviceId == id.DeviceId
                                                        && d.DeviceDescription.DeviceIdentity.VendorId == id.VendorId)
            ?? throw new ArgumentException($"Cannot find device '{id.DeviceId}' ({id.VendorId})");

        using MemoryStream stream = new();
        using (ZipArchive zipArchive = new(stream, ZipArchiveMode.Update, true))
            ioddStore.FillDeviceArchive(zipArchive, id);

        await stream.FlushAsync(cancellationToken);

        stream.Position = 0;
        var bytes = stream.ToArray();

        return new() { Content = bytes, Filename = device.Filename, };
    }
}
