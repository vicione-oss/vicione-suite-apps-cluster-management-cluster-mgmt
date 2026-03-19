using Sdk.Messaging;

namespace ClusterManagement.Public.Iodds.Requests;

public sealed record GetIODeviceImageRequest() : IRequest<GetIODeviceImageResponse>
{
    public required int VendorId { get; init; }
    public required int DeviceId { get; init; }
    public required string ImageFilename { get; init; }
}
