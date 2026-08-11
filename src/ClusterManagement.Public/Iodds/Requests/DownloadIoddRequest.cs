using Sdk.Messaging;

namespace ClusterManagement.Public.Iodds.Requests;

public sealed record DownloadIoddRequest() : IRequest<DownloadIoddResponse>
{
    public required int VendorId { get; set; }

    public required IReadOnlyList<int> DeviceIds { get; set; }
}
