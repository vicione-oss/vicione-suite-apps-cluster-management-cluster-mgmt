using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetClusterInfosRequest(Guid? ClusterId = null) : IRequest<GetClusterInfosResponse>;
