using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Iodds;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public class GetAutoIoddDownloadConsumer(IIoddStore ioddStore) : RequestConsumer<GetAutoIoddDownloadRequest, GetAutoIoddDownloadResponse>
{
    protected override Task<GetAutoIoddDownloadResponse> HandleException(ConsumeContext<GetAutoIoddDownloadRequest> context, Exception e)
        => Task.FromResult(new GetAutoIoddDownloadResponse() { RequestError = new ErrorInfo(500, $"{e.GetType()} {e.Message} {e.StackTrace}") });

    protected override Task<GetAutoIoddDownloadResponse> Respond(ConsumeContext<GetAutoIoddDownloadRequest> context)
        => Task.FromResult<GetAutoIoddDownloadResponse>(new() { IsAutoDownload = ioddStore.AutoDownloadIodds });
}
