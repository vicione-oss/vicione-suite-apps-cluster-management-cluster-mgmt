using System.IO.Abstractions;
using System.IO.Compression;
using ClusterManagement.Backend.Iodds.Extensions;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Commands;
using ClusterManagement.Public.Iodds.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public sealed partial class AddIoddConsumer(
    IIoddStore ioddStore,
    IFileSystem fileSystem,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    ILogger<AddIoddConsumer> logger) :
    IConsumer<AddIodd>
{
    public async Task Consume(ConsumeContext<AddIodd> context)
    {
        var command = context.Message;
        var size = 0L;

        // The archive is read and deleted afterwards, so only a file the upload handler stored is accepted.
        if (!fileSystem.IsUploadedIoddFile(workspaceProvider, command.Filename))
        {
            LogIoddFileOutsideUploadDirectory(logger, command.CorrelationId, command.Filename);

            await PublishError(context, IoddChanged.IoddFileOutsideUploadDirectory, "The IODD file is not in the upload directory.");
            return;
        }

        if (!fileSystem.File.Exists(command.Filename))
        {
            LogIoddFileNotFound(logger, command.CorrelationId, command.Filename);

            await PublishError(context, IoddChanged.IoddFileNotFound, $"The uploaded IODD file '{fileSystem.Path.GetFileName(command.Filename)}' was not found.");
            return;
        }

        try
        {
            LogAddingIodd(logger, command.CorrelationId, command.Filename);

            using var stream = fileSystem.File.Open(command.Filename, FileMode.Open, FileAccess.Read, FileShare.Read);
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
                CorrelationId = command.CorrelationId
            };
            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, ex, command.CorrelationId, command.Filename, size);

            await PublishError(context, IoddChanged.ExtractZipError, ex.Message);
        }

        try
        {
            LogDeletingIoddFile(logger, command.CorrelationId, command.Filename);

            fileSystem.File.Delete(command.Filename);
        }
        catch (Exception ex)
        {
            LogFileDeleteError(logger, ex, command.CorrelationId, command.Filename);
        }
    }

    private static async Task PublishError(ConsumeContext<AddIodd> context, int errorCode, string message)
    {
        var changeEvent = new IoddChanged(new IoddId(0, 0), null, CrudAction.Created, new ErrorInfo(errorCode, message))
        {
            CorrelationId = context.Message.CorrelationId
        };

        await context.Publish(changeEvent, context.CancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Adding IODD from file '{Filename}' CorrelationId={CorrelationId}")]
    private static partial void LogAddingIodd(ILogger<AddIoddConsumer> logger, Guid correlationId, string filename);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Added IODD vendorId='{VendorId}' device='{DeviceId}'")]
    private static partial void LogIoddAdded(ILogger<AddIoddConsumer> logger, int vendorId, int deviceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "IODD file '{Filename}' is not in the upload directory, ignoring it CorrelationId={CorrelationId}")]
    private static partial void LogIoddFileOutsideUploadDirectory(ILogger<AddIoddConsumer> logger, Guid correlationId, string filename);

    [LoggerMessage(Level = LogLevel.Error, Message = "IODD file '{Filename}' not found CorrelationId={CorrelationId}")]
    private static partial void LogIoddFileNotFound(ILogger<AddIoddConsumer> logger, Guid correlationId, string filename);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleting IODD file '{Filename}' CorrelationId={CorrelationId}")]
    private static partial void LogDeletingIoddFile(ILogger<AddIoddConsumer> logger, Guid correlationId, string filename);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to add IODD from file '{Filename}' ({Size} bytes) CorrelationId={CorrelationId}")]
    private static partial void LogError(ILogger<AddIoddConsumer> logger, Exception exception, Guid correlationId, string filename, long size);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete IODD file '{Filename}' CorrelationId={CorrelationId}")]
    private static partial void LogFileDeleteError(ILogger<AddIoddConsumer> logger, Exception exception, Guid correlationId, string filename);
}
