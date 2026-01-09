using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetActiveClusterRequest(Guid ClusterId) : IRequest<GetActiveClusterResponse>;
