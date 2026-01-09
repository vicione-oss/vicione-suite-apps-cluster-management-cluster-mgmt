using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

/// <summary>
/// to add some filter 
/// </summary>
public sealed record GetClusterDependenciesRequest : IRequest<GetClusterDependenciesResponse>;
