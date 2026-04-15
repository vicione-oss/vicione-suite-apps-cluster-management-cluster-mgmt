using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Commands;
using ClusterManagement.Public.Iodds.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public sealed partial class DeleteIoddConsumer(IIoddStore ioddStore, ILogger<DeleteIoddConsumer> logger) :
    IConsumer<DeleteIodd>
{
    public async Task Consume(ConsumeContext<DeleteIodd> context)
    {
        var id = new IoddId(context.Message.VendorId, context.Message.DeviceId);

        try
        {
            if (!ioddStore.TryRemoveDevice(id))
                throw new InvalidOperationException($"Failed to delete IODD {id}");

            var changeEvent = new IoddChanged(id, null, CrudAction.Deleted)
            {
                CorrelationId = context.Message.CorrelationId
            };
            await context.Publish(changeEvent, context.CancellationToken);
        }
        catch (Exception ex)
        {
            LogError(logger, ex, id);

            var errorInfo = new ErrorInfo(IoddChanged.DeleteIoddError, ex.Message);
            var changeEvent = new IoddChanged(id, null, CrudAction.Deleted, errorInfo)
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error deleting IODD {IoddId}")]
    private static partial void LogError(ILogger<DeleteIoddConsumer> logger, Exception exception, IoddId ioddId);
}
