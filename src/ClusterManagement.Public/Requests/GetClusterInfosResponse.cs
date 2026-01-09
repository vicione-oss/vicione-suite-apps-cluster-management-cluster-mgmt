using Sdk.Messaging;

namespace ClusterManagement.Public.Requests;

public sealed record GetClusterInfosResponse : IResponse
{
    public ErrorInfo? RequestError { get; init; }
    public List<ClusterInfo> Results { get; init; } = [];
}
