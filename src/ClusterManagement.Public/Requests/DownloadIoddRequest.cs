using ClusterManagement.Public.Iodds;
using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record DownloadIoddRequest() : IRequest<DownloadIoddResponse>
{
    public required IoddId Id { get; set; }
}
