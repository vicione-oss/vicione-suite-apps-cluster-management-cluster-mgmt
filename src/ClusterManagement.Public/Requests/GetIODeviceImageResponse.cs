using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public class GetIODeviceImageResponse : IResponse
{
    public required string ImageAsBase64 { get; init; }
    public ErrorInfo? RequestError { get; init; }
}
