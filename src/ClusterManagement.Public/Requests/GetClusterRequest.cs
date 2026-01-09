using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetClusterRequest(Guid ClusterId, Version? ClusterVersion) : IRequest<GetClusterResponse>;
