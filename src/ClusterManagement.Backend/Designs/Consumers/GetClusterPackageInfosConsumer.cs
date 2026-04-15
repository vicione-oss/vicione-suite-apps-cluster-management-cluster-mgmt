using ClusterManagement.Backend.EngineHosts.Consumers;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Events;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Semver;

namespace ClusterManagement.Backend.Designs.Consumers;


public sealed partial class GetClusterPackageInfosConsumer(
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

        LogError(logger, e, nameof(GetClusterPackageInfosRequest), coreVersion);

        return Task.FromResult(new GetClusterPackageInfosResponse([], coreVersion.ToString())
        {
            RequestError = new(ClusterError.UnknownError, e.Message),
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Consume {Request} failed for core version '{CoreVersion}'")]
    private static partial void LogError(ILogger<GetEngineHostPackageInfoConsumer> logger, Exception exception, string request, SemVersion? coreVersion);
}
