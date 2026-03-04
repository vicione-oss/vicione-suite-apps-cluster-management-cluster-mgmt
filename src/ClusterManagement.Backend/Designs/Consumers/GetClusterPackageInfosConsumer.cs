using ClusterManagement.Backend.EngineHosts.Consumers;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Designs.Consumers;


public sealed class GetClusterPackageInfosConsumer(
    IPackageInfoProvider packageInfoProvider,
    ILogger<GetEngineHostPackageInfoConsumer> logger) :
    RequestConsumer<GetClusterPackageInfosRequest, GetClusterPackageInfosResponse>
{
    protected override async Task<GetClusterPackageInfosResponse> Respond(ConsumeContext<GetClusterPackageInfosRequest> context)
    {
        var packageInfos = await packageInfoProvider.GetClusterPackageInfos(context.CancellationToken, context.Message.PackageNames);
        var coreVersion = packageInfoProvider.GetCoreVersion();

        return new GetClusterPackageInfosResponse(packageInfos.AsReadOnly(), coreVersion.ToString());
    }

    protected override Task<GetClusterPackageInfosResponse> HandleException(ConsumeContext<GetClusterPackageInfosRequest> context, Exception e)
    {
        var coreVersion = packageInfoProvider.GetCoreVersion();

        logger.LogError(e, "Consume {Request} failed for core version '{CoreVersion}'", nameof(GetClusterPackageInfosRequest), coreVersion);

        return Task.FromResult(new GetClusterPackageInfosResponse([], coreVersion.ToString())
        {
            RequestError = new(ClusterError.UnknownError, e.Message),
        });
    }
}
