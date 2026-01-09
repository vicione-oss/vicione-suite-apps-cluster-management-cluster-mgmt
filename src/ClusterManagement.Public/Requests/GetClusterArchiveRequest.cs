using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetClusterArchiveRequest(Guid ClusterId, Version ClusterVersion) : IRequest<GetClusterArchiveResponse>;
