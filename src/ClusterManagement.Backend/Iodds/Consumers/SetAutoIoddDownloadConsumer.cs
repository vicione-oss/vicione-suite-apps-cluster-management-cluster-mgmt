using ClusterManagement.Internal.Commands;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Events;
using MassTransit;

namespace ClusterManagement.Backend.Iodds.Consumers;

public sealed class SetAutoIoddDownloadConsumer(IIoddStore ioddStore) :
    IConsumer<SetAutoIoddDownload>
{
    public async Task Consume(ConsumeContext<SetAutoIoddDownload> context)
    {
        await ioddStore.SetAutoDownloadIodds(context.Message.Value);
        await context.Publish(new AutoIoddDownloadChanged(context.Message.Value));
    }
}
