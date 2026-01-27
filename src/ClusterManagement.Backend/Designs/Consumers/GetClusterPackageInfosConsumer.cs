using ClusterManagement.Backend.EngineHosts.Consumers;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Designs;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;
using Semver;

namespace ClusterManagement.Backend.Designs.Consumers;

public sealed class GetClusterPackageInfosConsumer(IDesignProvider designProvider, IPackageArtifactRepository repository, ILogger<GetEngineHostPackageInfoConsumer> logger) :
    RequestConsumer<GetClusterPackageInfosRequest, GetClusterPackageInfosResponse>
{
    protected override async Task<GetClusterPackageInfosResponse> Respond(ConsumeContext<GetClusterPackageInfosRequest> context)
    {
        var packageInfos = new List<ClusterPackageInfo>();
        var coreVersion = SemVersion.FromVersion(ClusterAssembliesResolver.GetViciOneCoreAssemblyVersion());

        // All packages we have already loaded
        var installed = await designProvider.GetClusterDependencies();
        foreach (var package in installed)
        {
            packageInfos.Add(new ClusterPackageInfo
            {
                Name = package.Name,
                MinCoreVersion = coreVersion.ToString(),
                InstalledVersion = SemVersion.FromVersion(package.Version).ToString(),
            });
        }

        // All cluster packages available on jfrog for referenced core version
        var available = await repository.QueryMetadataArtifacts(coreVersion, null, context.CancellationToken);

        // Create Dictionary<PackageName, IEnumerable<Version>>:
        var emptyVersion = "0.0.0";
        var grouped = available.GroupBy(k => k.Path)
                .Select(k => (k.Key, k.Select(a => MetadataArtifactNameRegex.ParseVersion(a.Name, out var packageVersion) ? packageVersion.ToString() : emptyVersion)
                    .Where(k => k != emptyVersion)
                    .Distinct()))
                .ToDictionary(k => Path.GetFileName(k.Key), v => v.Item2);

        foreach (var packageName in grouped.Keys)
        {
            // Not installed yet - add it
            var existing = packageInfos.FirstOrDefault(k => k.Name == packageName);
            if (existing is null)
            {
                packageInfos.Add(new ClusterPackageInfo
                {
                    Name = packageName,
                    MinCoreVersion = coreVersion.ToString(),
                    Versions = grouped[packageName].ToList().AsReadOnly(),
                });
                continue;
            }

            // Update installed one - won't have versions yet
            existing.Versions = grouped[packageName].ToList().AsReadOnly();
        }

        return new GetClusterPackageInfosResponse(packageInfos.AsReadOnly());
    }

    protected override Task<GetClusterPackageInfosResponse> HandleException(ConsumeContext<GetClusterPackageInfosRequest> context, Exception e)
    {
        logger.LogError(e, "Consume {Request} failed", nameof(GetClusterPackageInfosRequest));

        return Task.FromResult(new GetClusterPackageInfosResponse([])
        {
            RequestError = new(ClusterError.UnknownError, e.Message),
        });
    }
}
