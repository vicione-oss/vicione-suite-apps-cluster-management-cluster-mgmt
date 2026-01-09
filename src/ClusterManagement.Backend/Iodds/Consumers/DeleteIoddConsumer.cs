using ClusterManagement.Public.Commands;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Iodds;
using MassTransit;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public sealed class DeleteIoddConsumer(ISuiteMediator mediator, IIoddStore ioddStore) :
    IConsumer<DeleteIodd>
{
    public async Task Consume(ConsumeContext<DeleteIodd> context)
    {
        var id = new IoddId(context.Message.VendorId, context.Message.DeviceId);

        if (ioddStore.TryRemoveDevice(id))
            await mediator.Publish(new IoddChanged(id, null, CrudAction.Deleted));
        else
            await mediator.Publish(new IoddError("Delete Failed", new ErrorInfo(IoddError.DeleteIoddError, "Failed to delete IODD")));
    }
}
