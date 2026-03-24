using Sdk.Messaging;

namespace ClusterManagement.Public.Iodds.Requests;

public class GetIODevicesResponse : IResponse
{
    public required IEnumerable<DeviceInfo> IODevices { get; init; }
    public ErrorInfo? RequestError { get; init; }
}
