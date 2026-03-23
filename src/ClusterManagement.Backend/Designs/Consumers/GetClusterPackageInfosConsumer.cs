using ClusterManagement.Backend.EngineHosts.Consumers;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Events;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Designs.Consumers;


public sealed class GetClusterPackageInfosConsumer(
    IPackageInfoProvider packageInfoProvider,
    ILogger<GetEngineHostPackageInfoConsumer> logger) :
    RequestConsumer<GetClusterPackageInfosRequest, GetClusterPackageInfosResponse>
{
    public override async Task<GetClusterPackageInfosResponse> Respond(GetClusterPackageInfosRequest message, CancellationToken cancellationToken)
    {
        var packageInfos = await packageInfoProvider.GetClusterPackageInfos(cancellationToken, message.PackageNames);
        var coreVersion = packageInfoProvider.GetCoreVersion();

        return new GetClusterPackageInfosResponse(packageInfos.AsReadOnly(), coreVersion.ToString());
    }

    public override Task<GetClusterPackageInfosResponse> HandleException(GetClusterPackageInfosRequest message, Exception e, CancellationToken cancellationToken)
    {
        var coreVersion = packageInfoProvider.GetCoreVersion();

        logger.LogError(e, "Consume {Request} failed for core version '{CoreVersion}'", nameof(GetClusterPackageInfosRequest), coreVersion);

        return Task.FromResult(new GetClusterPackageInfosResponse([], coreVersion.ToString())
        {
            RequestError = new(ClusterError.UnknownError, e.Message),
        });
    }
}
