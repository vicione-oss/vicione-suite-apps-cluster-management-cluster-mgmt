using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetIODeviceImageConsumerRequest() : IRequest<GetIODeviceImageResponse>
{
    public required int VendorId { get; init; }
    public required int DeviceId { get; init; }
    public required string ImageFilename { get; init; }
}
