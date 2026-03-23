using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Iodds;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Iodds.Consumers;

public class GetAutoIoddDownloadConsumer(IIoddStore ioddStore) : RequestConsumer<GetAutoIoddDownloadRequest, GetAutoIoddDownloadResponse>
{
    public override Task<GetAutoIoddDownloadResponse> HandleException(GetAutoIoddDownloadRequest message, Exception e, CancellationToken cancellationToken)
        => Task.FromResult(new GetAutoIoddDownloadResponse() { RequestError = new ErrorInfo(500, $"{e.GetType()} {e.Message} {e.StackTrace}") });

    public override Task<GetAutoIoddDownloadResponse> Respond(GetAutoIoddDownloadRequest message, CancellationToken cancellationToken)
        => Task.FromResult<GetAutoIoddDownloadResponse>(new() { IsAutoDownload = ioddStore.GetAutoDownloadIodds() });
}
