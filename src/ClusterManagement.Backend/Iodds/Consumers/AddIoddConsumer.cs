using System.IO.Compression;
using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Commands;
using ClusterManagement.Public.Iodds.Events;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public sealed class AddIoddConsumer(IIoddStore ioddStore) :
    IConsumer<AddIodd>, IConsumer<Fault<AddIodd>>
{
    public async Task Consume(ConsumeContext<AddIodd> context)
    {
        using MemoryStream stream = new(context.Message.Data, 0, context.Message.Data.Length);
        using ZipArchive zipArchive = new(stream);

        if (ioddStore.TryAddDevice(zipArchive, out var id))
        {
            var newDevice = ioddStore.Devices.First(d => d.DeviceDescription.DeviceIdentity.VendorId == id.VendorId
                                                        && d.DeviceDescription.DeviceIdentity.DeviceId == id.DeviceId);

            await context.Publish(new IoddChanged(id, newDevice, CrudAction.Created));
        }
        else
        {
            throw new InvalidOperationException("Failed to add IODD, does it already exist?");
        }
    }

    public async Task Consume(ConsumeContext<Fault<AddIodd>> context)
        => await context.Publish(new IoddError("Could not add IODD.", new ErrorInfo(IoddError.ExtractZipError, null)));
}
