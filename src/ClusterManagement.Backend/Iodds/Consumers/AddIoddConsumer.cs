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

            var result = ioddStore.AddDevice(zipArchive, out var id);

            switch (result)
            {
                case AddDeviceResult.Added when id is not null:
                    var newDevice = FindDevice(id)
                        ?? throw new InvalidOperationException($"The added IODD for vendor id {id.VendorId} and device id {id.DeviceId} is not in the store.");

                    LogIoddAdded(logger,
                        newDevice.DeviceDescription.DeviceIdentity.VendorId,
                        newDevice.DeviceDescription.DeviceIdentity.DeviceId);

                    var changeEvent = new IoddChanged(id, newDevice, CrudAction.Created)
                    {
                        CorrelationId = command.CorrelationId
                    };
                    await context.Publish(changeEvent, context.CancellationToken);
                    break;

                case AddDeviceResult.AlreadyExists when id is not null:
                    LogIoddAlreadyExists(logger, command.CorrelationId, id.VendorId, id.DeviceId);

                    // The existing device lets the client name it in its message.
                    await PublishError(context, IoddChanged.IoddAlreadyExists,
                        $"An IODD for vendor id {id.VendorId} and device id {id.DeviceId} already exists.",
                        id, FindDevice(id));
                    break;

                case AddDeviceResult.ArchiveTooLarge:
                    LogArchiveTooLarge(logger, command.CorrelationId, command.Filename, size);

                    await PublishError(context, IoddChanged.ArchiveTooLarge, "The IODD archive exceeds the configured archive limits.");
                    break;

                default:
                    // An expected outcome such as an archive without an IODD file, so no error with a stack trace.
                    LogIoddNotAdded(logger, command.CorrelationId, command.Filename, result);

                    await PublishError(context, IoddChanged.ExtractZipError, $"Failed to add IODD: {result}.");
                    break;
            }
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

    private DeviceInfo? FindDevice(IoddId id)
        => ioddStore.Devices.FirstOrDefault(d => d.DeviceDescription.DeviceIdentity.VendorId == id.VendorId
                                                 && d.DeviceDescription.DeviceIdentity.DeviceId == id.DeviceId);

    private static async Task PublishError(ConsumeContext<AddIodd> context, int errorCode, string message, IoddId? id = null, DeviceInfo? deviceInfo = null)
    {
        var changeEvent = new IoddChanged(id ?? new IoddId(0, 0), deviceInfo, CrudAction.Created, new ErrorInfo(errorCode, message))
        {
            CorrelationId = context.Message.CorrelationId
        };

        await context.Publish(changeEvent, context.CancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Adding IODD from file '{Filename}' CorrelationId={CorrelationId}")]
    private static partial void LogAddingIodd(ILogger<AddIoddConsumer> logger, Guid correlationId, string filename);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Added IODD vendorId='{VendorId}' device='{DeviceId}'")]
    private static partial void LogIoddAdded(ILogger<AddIoddConsumer> logger, int vendorId, int deviceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "IODD vendorId='{VendorId}' device='{DeviceId}' already exists, not adding it CorrelationId={CorrelationId}")]
    private static partial void LogIoddAlreadyExists(ILogger<AddIoddConsumer> logger, Guid correlationId, int vendorId, int deviceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "IODD from file '{Filename}' was not added: {Result} CorrelationId={CorrelationId}")]
    private static partial void LogIoddNotAdded(ILogger<AddIoddConsumer> logger, Guid correlationId, string filename, AddDeviceResult result);

    [LoggerMessage(Level = LogLevel.Warning, Message = "IODD file '{Filename}' is not in the upload directory, ignoring it CorrelationId={CorrelationId}")]
    private static partial void LogIoddFileOutsideUploadDirectory(ILogger<AddIoddConsumer> logger, Guid correlationId, string filename);

    [LoggerMessage(Level = LogLevel.Error, Message = "IODD file '{Filename}' not found CorrelationId={CorrelationId}")]
    private static partial void LogIoddFileNotFound(ILogger<AddIoddConsumer> logger, Guid correlationId, string filename);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleting IODD file '{Filename}' CorrelationId={CorrelationId}")]
    private static partial void LogDeletingIoddFile(ILogger<AddIoddConsumer> logger, Guid correlationId, string filename);

    [LoggerMessage(Level = LogLevel.Warning, Message = "IODD file '{Filename}' ({Size} bytes) is too large to extract CorrelationId={CorrelationId}")]
    private static partial void LogArchiveTooLarge(ILogger<AddIoddConsumer> logger, Guid correlationId, string filename, long size);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to add IODD from file '{Filename}' ({Size} bytes) CorrelationId={CorrelationId}")]
    private static partial void LogError(ILogger<AddIoddConsumer> logger, Exception exception, Guid correlationId, string filename, long size);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete IODD file '{Filename}' CorrelationId={CorrelationId}")]
    private static partial void LogFileDeleteError(ILogger<AddIoddConsumer> logger, Exception exception, Guid correlationId, string filename);
}
