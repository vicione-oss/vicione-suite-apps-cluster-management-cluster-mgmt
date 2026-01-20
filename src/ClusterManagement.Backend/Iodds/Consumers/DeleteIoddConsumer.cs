using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Commands;
using ClusterManagement.Public.Iodds.Events;
using MassTransit;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public sealed class DeleteIoddConsumer(IIoddStore ioddStore) :
    IConsumer<DeleteIodd>
{
    public async Task Consume(ConsumeContext<DeleteIodd> context)
    {
        var id = new IoddId(context.Message.VendorId, context.Message.DeviceId);

        if (ioddStore.TryRemoveDevice(id))
            await context.Publish(new IoddChanged(id, null, CrudAction.Deleted));
        else
            await context.Publish(new IoddError("Delete Failed", new ErrorInfo(IoddError.DeleteIoddError, "Failed to delete IODD")));
    }
}
