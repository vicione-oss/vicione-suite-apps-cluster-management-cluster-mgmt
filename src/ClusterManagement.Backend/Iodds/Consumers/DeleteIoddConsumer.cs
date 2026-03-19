using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Commands;
using ClusterManagement.Public.Iodds.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public sealed class DeleteIoddConsumer(IIoddStore ioddStore, ILogger<DeleteIoddConsumer> logger) :
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
            logger.LogError(ex, "Error deleting IODD {IoddId}", id);

            var errorInfo = new ErrorInfo(IoddChanged.DeleteIoddError, ex.Message);
            var changeEvent = new IoddChanged(id, null, CrudAction.Deleted, errorInfo)
            {
                CorrelationId = context.Message.CorrelationId
            };

            await context.Publish(changeEvent, context.CancellationToken);
        }
    }
}
