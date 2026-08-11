using System.Globalization;
using System.IO.Compression;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public sealed partial class DownloadIoddConsumer(IIoddStore ioddStore, ILogger<DownloadIoddConsumer> logger) :
    RequestConsumer<DownloadIoddRequest, DownloadIoddResponse>
{
    public override async Task<DownloadIoddResponse> Respond(DownloadIoddRequest message, CancellationToken cancellationToken)
    {
        var vendorId = message.VendorId;
        var devices = ioddStore.Devices
                .Where(d => d.DeviceDescription.DeviceIdentity.VendorId == vendorId
                         && message.DeviceIds.Contains(d.DeviceDescription.DeviceIdentity.DeviceId))
                .ToList();

        if (devices.Count == 0)
            throw new ArgumentException($"Cannot find any device of vendor '{vendorId}'");

        LogDownloading(logger, vendorId);

        using MemoryStream stream = new();
        IReadOnlyCollection<int> archivedDeviceIds;
        using (ZipArchive zipArchive = new(stream, ZipArchiveMode.Update, true))
            archivedDeviceIds = ioddStore.FillVendorArchive(zipArchive, vendorId, devices.Select(d => d.DeviceDescription.DeviceIdentity.DeviceId));

        if (archivedDeviceIds.Count == 0)
            throw new InvalidOperationException($"No IODD of vendor '{vendorId}' could be archived");

        await stream.FlushAsync(cancellationToken);

        stream.Position = 0;
        var bytes = stream.ToArray();

        return new() { Content = bytes, Filename = GetArchiveName(vendorId, devices[0].DeviceDescription.DeviceIdentity.VendorName), };
    }

    public override Task<DownloadIoddResponse> HandleException(DownloadIoddRequest context, Exception e, CancellationToken cancellationToken)
    {
        LogError(logger, e, context.VendorId);

        return Task.FromResult(new DownloadIoddResponse() { RequestError = new ErrorInfo(400, "failed to download iodd") });
    }
    private static string GetArchiveName(int vendorId, string vendorName)
    {
        var name = string.IsNullOrWhiteSpace(vendorName)
            ? vendorId.ToString(CultureInfo.InvariantCulture)
            : vendorName.Trim();

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            name = name.Replace(invalidChar, '_');

        return $"iodds-{name}.zip";
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Downloading IODDs for vendor '{VendorId}'")]
    private static partial void LogDownloading(ILogger<DownloadIoddConsumer> logger, int vendorId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to download IODD for vendor '{VendorId}'")]
    private static partial void LogError(ILogger<DownloadIoddConsumer> logger, Exception exception, int vendorId);
}
