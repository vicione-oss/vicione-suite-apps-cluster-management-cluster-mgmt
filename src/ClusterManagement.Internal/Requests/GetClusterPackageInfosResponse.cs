using ClusterManagement.Internal.Contracts;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

public sealed record GetClusterPackageInfosResponse(IReadOnlyCollection<ClusterPackageInfo> PackageInfos) : IResponse
{
    public ErrorInfo? RequestError { get; init; }
}
