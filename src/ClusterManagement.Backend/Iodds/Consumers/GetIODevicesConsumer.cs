using ClusterManagement.Public.Iodds;
using ClusterManagement.Public.Iodds.Requests;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public class GetIODevicesConsumer(IIoddStore ioddStore) : RequestConsumer<GetIODevicesRequest, GetIODevicesResponse>
{
    public override Task<GetIODevicesResponse> HandleException(GetIODevicesRequest message, Exception e, CancellationToken cancellationToken)
        => Task.FromResult(new GetIODevicesResponse() { RequestError = new ErrorInfo(500, $"{e.GetType()} {e.Message} {e.StackTrace}"), IODevices = [] });

    public override Task<GetIODevicesResponse> Respond(GetIODevicesRequest message, CancellationToken cancellationToken)
        => Task.FromResult(new GetIODevicesResponse() { IODevices = ioddStore.Devices });
}
