using System.IO.Compression;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Commands;
using ClusterManagement.Public.Iodds.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public sealed partial class AddIoddConsumer(IIoddStore ioddStore, ILogger<AddIoddConsumer> logger) :
    IConsumer<AddIodd>
{
    public async Task Consume(ConsumeContext<AddIodd> context)
    {
        try
        {
            using MemoryStream stream = new(context.Message.Data, 0, context.Message.Data.Length);
            using ZipArchive zipArchive = new(stream);

            if (!ioddStore.TryAddDevice(zipArchive, out var id))
                throw new InvalidOperationException("Failed to add IODD, does it already exist?");

            var newDevice = ioddStore.Devices.First(d => d.DeviceDescription.DeviceIdentity.VendorId == id.VendorId
                                                        && d.DeviceDescription.DeviceIdentity.DeviceId == id.DeviceId);

            var changeEvent = new IoddChanged(id, newDevice, CrudAction.Created)
            {
                CorrelationId = context.Message.CorrelationId
            };
            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, context.Message.Data.Length);

            var errorInfo = new ErrorInfo(IoddChanged.ExtractZipError, ex.Message);
            var changeEvent = new IoddChanged(new IoddId(0, 0), null, CrudAction.Created, errorInfo)
            {
                CorrelationId = context.Message.CorrelationId
            };
            await context.Publish(changeEvent, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to add IODD, invalid zip file with length: {Length}.")]
    private static partial void LogError(ILogger<AddIoddConsumer> logger, int length);
}
