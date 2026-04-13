using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetResourcesDownloadStatusResponse : IResponse
{
    public ErrorInfo? RequestError { get; init; }
    public bool IsCompleted { get; init; }
}
