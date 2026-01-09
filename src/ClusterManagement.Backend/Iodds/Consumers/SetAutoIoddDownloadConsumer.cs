using ClusterManagement.Internal.Commands;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Iodds;
using MassTransit;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public sealed class SetAutoIoddDownloadConsumer(ISuiteMediator mediator, IIoddStore ioddStore) :
    IConsumer<SetAutoIoddDownload>
{
    public async Task Consume(ConsumeContext<SetAutoIoddDownload> context)
    {
        ioddStore.AutoDownloadIodds = context.Message.Value;
        await mediator.Publish(new AutoIoddDownloadChanged(context.Message.Value));
    }
}
