using System.IO.Abstractions;
using System.IO.Compression;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Commands;
using ClusterManagement.Public.Iodds.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public sealed partial class AddIoddConsumer(IIoddStore ioddStore, IFileSystem fileSystem, ILogger<AddIoddConsumer> logger) :
    IConsumer<AddIodd>
{
    public async Task Consume(ConsumeContext<AddIodd> context)
    {
        var size = 0L;

        try
        {
            LogAddingIodd(logger, context.Message.Filename);

            using var stream = fileSystem.File.Open(context.Message.Filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            size = stream.Length;
            using ZipArchive zipArchive = new(stream);

            if (!ioddStore.TryAddDevice(zipArchive, out var id))
                throw new InvalidOperationException("Failed to add IODD, does it already exist?");

            var newDevice = ioddStore.Devices.First(d => d.DeviceDescription.DeviceIdentity.VendorId == id.VendorId
                                                        && d.DeviceDescription.DeviceIdentity.DeviceId == id.DeviceId);

            LogIoddAdded(logger,
                newDevice.DeviceDescription.DeviceIdentity.VendorId,
                newDevice.DeviceDescription.DeviceIdentity.DeviceId);

            var changeEvent = new IoddChanged(id, newDevice, CrudAction.Created)
            {
                CorrelationId = context.Message.CorrelationId
            };
            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, context.Message.Filename, size);

            var errorInfo = new ErrorInfo(IoddChanged.ExtractZipError, ex.Message);
            var changeEvent = new IoddChanged(new IoddId(0, 0), null, CrudAction.Created, errorInfo)
            {
                CorrelationId = context.Message.CorrelationId
            };
            await context.Publish(changeEvent, context.CancellationToken);
        }

        try
        {
            LogDeletingIoddFile(logger, context.Message.Filename);

            fileSystem.File.Delete(context.Message.Filename);
        }
        catch (Exception ex)
        {
            LogFileDeleteError(logger, context.Message.Filename, ex.GetType().Name, ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Adding IODD from file '{filename}'")]
    private static partial void LogAddingIodd(ILogger<AddIoddConsumer> logger, string filename);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Added IODD vendorId='{vendorId}' device='{deviceId}'")]
    private static partial void LogIoddAdded(ILogger<AddIoddConsumer> logger, int vendorId, int deviceId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleting IODD file '{filename}'")]
    private static partial void LogDeletingIoddFile(ILogger<AddIoddConsumer> logger, string filename);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to add IODD, invalid zip file {filename} ({size}) bytes.")]
    private static partial void LogError(ILogger<AddIoddConsumer> logger, string filename, long size);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete IODD file {filename}: {ex}, {message}")]
    private static partial void LogFileDeleteError(ILogger<AddIoddConsumer> logger, string filename, string ex, string message);
}
