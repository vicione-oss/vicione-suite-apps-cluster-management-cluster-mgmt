using ClusterManagement.Backend.Designs;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Internal;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Messaging;
using Semver;

namespace ClusterManagement.Backend.EngineHosts.Consumers;

public sealed class GetEngineHostPackageInfoConsumer(IEngineHostArtifactRepository repository, IOptions<ClusterManagementOptions> options, ILogger<GetEngineHostPackageInfoConsumer> logger) :
    RequestConsumer<GetEngineHostPackageInfoRequest, GetEngineHostPackageInfoResponse>
{
    private readonly ClusterManagementOptions _options = options.Value;
    private const string PackageName = "EngineHost";

    protected override async Task<GetEngineHostPackageInfoResponse> Respond(ConsumeContext<GetEngineHostPackageInfoRequest> context)
    {
        var coreVersion = ClusterAssembliesResolver.GetViciOneCoreAssemblyVersion();
        var installedVersion = _options.GetEngineHostVersion();

        // All EngineHost packages available on jfrog for referenced core version
        var engineHosts = await repository.QueryMetadataArtifacts(SemVersion.FromVersion(coreVersion), context.CancellationToken);

        // We could now load the zip artifacts but just for the version we spare the requests and extract it from name
        var packageInfo = ClusterPackageInfo(installedVersion, coreVersion);
        var emptyVersion = new SemVersion(0, 0, 0);
        packageInfo.Versions = engineHosts
            .Select(k => MetadataArtifactNameRegex.ParseVersion(k.Name, out var packageVersion) ? packageVersion : emptyVersion)
            .Where(v => v != emptyVersion)
            .Select(v => v.ToString()!)
            .ToHashSet();

        return new GetEngineHostPackageInfoResponse(packageInfo);
    }

    protected override Task<GetEngineHostPackageInfoResponse> HandleException(ConsumeContext<GetEngineHostPackageInfoRequest> context, Exception e)
    {
        logger.LogError(e, "Consume {Request} failed", nameof(GetEngineHostPackageInfoRequest));

        var coreVersion = ClusterAssembliesResolver.GetViciOneCoreAssemblyVersion();
        var installedVersion = _options.GetEngineHostVersion();
        var info = ClusterPackageInfo(installedVersion, coreVersion);

        return Task.FromResult(new GetEngineHostPackageInfoResponse(info)
        {
            RequestError = new(ClusterError.UnknownError, e.Message),
        });
    }

    private static ClusterPackageInfo ClusterPackageInfo(SemVersion installedVersion, Version coreVersion)
        => new()
        {
            Name = PackageName,
            InstalledVersion = installedVersion.ToString(),
            MinCoreVersion = coreVersion.ToString()
        };
}
