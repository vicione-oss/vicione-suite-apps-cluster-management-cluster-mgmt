using Sdk.Messaging;

namespace ClusterManagement.Public.Iodds.Requests;

public sealed record GetIODevicesRequest() : IRequest<GetIODevicesResponse>
{ }
