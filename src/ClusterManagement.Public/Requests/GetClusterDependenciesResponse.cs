using Sdk.Messaging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Public.Requests;

public sealed class GetClusterDependenciesResponse : IResponse
{
    public List<ClusterDependency> Dependencies { get; init; } = [];
    public ErrorInfo? RequestError { get; init; }
}
