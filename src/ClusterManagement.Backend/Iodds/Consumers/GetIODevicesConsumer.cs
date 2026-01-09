using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Requests;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public class GetIODevicesConsumer(IIoddStore ioddStore) : RequestConsumer<GetIODevicesRequest, GetIODevicesResponse>
{
    protected override Task<GetIODevicesResponse> HandleException(ConsumeContext<GetIODevicesRequest> context, Exception e)
        => Task.FromResult(new GetIODevicesResponse() { RequestError = new ErrorInfo(500, $"{e.GetType()} {e.Message} {e.StackTrace}"), IODevices = [] });

    protected override Task<GetIODevicesResponse> Respond(ConsumeContext<GetIODevicesRequest> context)
        => Task.FromResult(new GetIODevicesResponse() { IODevices = ioddStore.Devices });
}
