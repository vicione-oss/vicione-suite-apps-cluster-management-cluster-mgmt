using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetDeployClusterStateRequest(Guid ClusterId) : IRequest<GetDeployClusterStateResponse>;
