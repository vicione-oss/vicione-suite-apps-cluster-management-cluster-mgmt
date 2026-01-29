using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

public record GetClusterPackageInfosRequest(List<string>? PackageNames = null) : IRequest<GetClusterPackageInfosResponse>;
