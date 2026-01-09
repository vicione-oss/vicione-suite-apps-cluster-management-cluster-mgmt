using ClusterManagement.Internal.Contracts;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

public sealed record GetEngineHostPackageInfoResponse(ClusterPackageInfo PackageInfo) : IResponse
{
    public ErrorInfo? RequestError { get; init; }
}
