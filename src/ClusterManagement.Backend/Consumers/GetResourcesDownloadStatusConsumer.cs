using ClusterManagement.Public.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed class GetResourcesDownloadStatusConsumer(ILogger<GetResourcesDownloadStatusConsumer> logger) :
    RequestConsumer<GetResourcesDownloadStatusRequest, GetResourcesDownloadStatusResponse>
{
    public override Task<GetResourcesDownloadStatusResponse> Respond(
        GetResourcesDownloadStatusRequest message,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new GetResourcesDownloadStatusResponse
        {
            IsCompleted = ResourceDownloadCompletedService.IsCompleted
        });
    }

    public override Task<GetResourcesDownloadStatusResponse> HandleException(
        GetResourcesDownloadStatusRequest message,
        Exception e,
        CancellationToken cancellationToken)
    {
        logger.LogHandleException(e, nameof(GetResourcesDownloadStatusRequest));

        return Task.FromResult(new GetResourcesDownloadStatusResponse
        {
            RequestError = new ErrorInfo(500, e.Message)
        });
    }
}
