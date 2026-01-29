using ClusterManagement.Internal.Contracts;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

public sealed record GetClusterPackageInfosResponse(IReadOnlyCollection<ClusterPackageInfo> PackageInfos, string MinCoreVersion) : IResponse
{
    public ErrorInfo? RequestError { get; init; }
}
