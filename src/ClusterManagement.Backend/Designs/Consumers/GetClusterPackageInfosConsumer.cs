using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Backend.EngineHosts.Consumers;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Backend.Snapshots.Extensions;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Requests;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Artifacts;
using Sdk.Messaging;
using Semver;

namespace ClusterManagement.Backend.Designs.Consumers;

public sealed class GetClusterPackageInfosConsumer(
    IClusterDependencyStore clusterDependencyStore,
    IClusterSnapshotStore clusterSnapshotStore,
    IClusterDbContext dbContext,
    IPackageContentProvider packageContentProvider,
    IPackageArtifactRepository repository,
    ILogger<GetEngineHostPackageInfoConsumer> logger) :
    RequestConsumer<GetClusterPackageInfosRequest, GetClusterPackageInfosResponse>
{
    protected override async Task<GetClusterPackageInfosResponse> Respond(ConsumeContext<GetClusterPackageInfosRequest> context)
    {
        var packageInfos = new List<ClusterPackageInfo>();
        var coreVersion = SemVersion.FromVersion(ClusterAssembliesResolver.GetViciOneCoreAssemblyVersion());
        var packagesOfInterest = context.Message.PackageNames;

        // Add packages we have already loaded
        packageInfos.AddRange(GetInstalledPackages(packagesOfInterest));

        // Add cluster packages available on jfrog for referenced core version 
        await AddAvailablePackageVersions(packageInfos, packagesOfInterest, coreVersion, context.CancellationToken);

        // Additionally there might be outstanding operations enqueued for next startup already
        await AddClusterPackageOperations(packageInfos, packagesOfInterest, context.CancellationToken);

        // Add package versions from active cluster dependencies
        await AddActiveClusterDependencies(packageInfos, packagesOfInterest, context.CancellationToken);

        // There might be manual uploaded packages that are not installed and not available        
        AddLocallyStoredPackages(packageInfos, packagesOfInterest);

        // Provide information of available updates
        UpdateLatestVersion(packageInfos);

        // Sort package infos by name
        packageInfos.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        return new GetClusterPackageInfosResponse(packageInfos.AsReadOnly(), coreVersion.ToString());
    }

    protected override Task<GetClusterPackageInfosResponse> HandleException(ConsumeContext<GetClusterPackageInfosRequest> context, Exception e)
    {
        var coreVersion = SemVersion.FromVersion(ClusterAssembliesResolver.GetViciOneCoreAssemblyVersion());

        logger.LogError(e, "Consume {Request} failed for core version '{CoreVersion}'", nameof(GetClusterPackageInfosRequest), coreVersion);

        return Task.FromResult(new GetClusterPackageInfosResponse([], coreVersion.ToString())
        {
            RequestError = new(ClusterError.UnknownError, e.Message),
        });
    }

    private IEnumerable<ClusterPackageInfo> GetInstalledPackages(List<string>? packagesOfInterest)
    {
        var installed = packageContentProvider.GetPackageContents()
            .Where(k => packagesOfInterest is null || packagesOfInterest.Contains(k.Key.Name));

        return installed.Select(package => new ClusterPackageInfo
        {
            Name = package.Key.Name,
            InstalledVersion = SemVersion.FromVersion(package.Key.Version).ToString(),
            Error = package.Value.Error is not null
                ? new ErrorInfo(package.Value.Error.HResult, package.Value.Error.Message)
                : null,
        });
    }

    private async Task AddAvailablePackageVersions(List<ClusterPackageInfo> packageInfos, List<string>? packagesOfInterest, SemVersion coreVersion, CancellationToken cancellationToken)
    {
        if (packagesOfInterest is null || packagesOfInterest.Count == 0)
        {
            // All cluster packages available on jfrog for referenced core version
            var available = await repository.QueryMetadataArtifacts(coreVersion, null, cancellationToken);

            UpdateAvailablePackageVersions(packageInfos, available);
        }
        else
        {
            // Query only the artifacts for the wanted packages
            var availableTasks = packagesOfInterest.Select(k => repository.QueryMetadataArtifacts(coreVersion, k, cancellationToken));
            var availableResults = await Task.WhenAll(availableTasks);
            var available = availableResults.SelectMany(k => k);

            UpdateAvailablePackageVersions(packageInfos, available);
        }
    }

    private async void UpdateAvailablePackageVersions(List<ClusterPackageInfo> packageInfos, IEnumerable<IArtifact> available)
    {
        var emptyVersion = "0.0.0";

        // Create Dictionary<PackageName, IEnumerable<Version>>:
        var availablePackages = available.GroupBy(k => k.Path)
                .Select(k => (k.Key, k.Select(a => MetadataArtifactNameRegex.ParseVersion(a.Name, out var packageVersion) ? packageVersion.ToString() : emptyVersion)
                    .Where(k => k != emptyVersion)
                    .Distinct()))
                .ToDictionary(k => Path.GetFileName(k.Key), v => v.Item2);

        foreach (var packageName in availablePackages.Keys)
        {
            // Not installed yet - add it
            var existing = packageInfos.FirstOrDefault(k => k.Name == packageName);
            if (existing is null)
            {
                var packageInfo = new ClusterPackageInfo
                {
                    Name = packageName,
                    Versions = availablePackages[packageName].ToList().AsReadOnly(),
                };

                packageInfos.Add(packageInfo);

                logger.LogDebug("Add artifact package {Package} available versions: {Versions}", packageInfo.Name, string.Join(", ", packageInfo.Versions));
                continue;
            }

            // Update installed one - won't have versions yet
            existing.Versions = availablePackages[packageName].ToList().AsReadOnly();
        }
    }

    private async Task AddClusterPackageOperations(List<ClusterPackageInfo> packageInfos, List<string>? packagesOfInterest, CancellationToken cancellationToken)
    {
        var operations = (await clusterDependencyStore.GetEnqueuedOperations(cancellationToken))
            .Where(k => packagesOfInterest == null || packagesOfInterest.Contains(k.Dependency.Name))
            .ToList();

        foreach (var operation in operations)
        {
            // If the package is not known yet, add it
            var packageInfo = packageInfos.FirstOrDefault(p => p.Name == operation.Dependency.Name);
            if (packageInfo is null)
            {
                packageInfo = new ClusterPackageInfo
                {
                    Name = operation.Dependency.Name,
                };
                packageInfos.Add(packageInfo);

                logger.LogDebug("Add package {Package} from pending operation {Op} version {Version}",
                    packageInfo.Name, operation.OperationKind, operation.Dependency.Version);
            }

            // Apply pending operation
            packageInfo.PendingOperation = operation;
        }
    }

    private async Task AddActiveClusterDependencies(List<ClusterPackageInfo> packageInfos, List<string>? packagesOfInterest, CancellationToken cancellationToken)
    {
        var request = new GetClusterInfosRequest();
        var activeClusterInfos = await dbContext.ActiveClusters.AsNoTracking().ToListAsync(cancellationToken);

        foreach (var clusterInfo in activeClusterInfos)
        {
            // We don't deserialize the whole cluster but just read the relevant nodes to be faster
            await clusterSnapshotStore.HandleClusterDependencies(clusterInfo.Id, clusterInfo.Version, (_, name, version) =>
            {
                if (packagesOfInterest is not null && !packagesOfInterest.Contains(name))
                    return;

                // If we don't have the package yet, cluster won't work so show it as broken
                var packageInfo = packageInfos.FirstOrDefault(p => p.Name == name);
                if (packageInfo is null)
                {
                    packageInfo = new ClusterPackageInfo
                    {
                        Name = name,
                        RunningClusterVersion = SemVersion.FromVersion(version).ToString()
                    };
                    packageInfos.Add(packageInfo);

                    logger.LogDebug("Add active cluster package {Package} with version {Versions}", packageInfo.Name, packageInfo.RunningClusterVersion);
                    return;
                }

                packageInfo.RunningClusterVersion = SemVersion.FromVersion(version).ToString();

            }, cancellationToken);
        }
    }

    private void AddLocallyStoredPackages(List<ClusterPackageInfo> packageInfos, List<string>? packagesOfInterest)
    {
        var localPackages = clusterDependencyStore.BuildStoredPackageVersionMap();

        foreach (var localPackage in localPackages)
        {
            if (packagesOfInterest is not null && !packagesOfInterest.Contains(localPackage.Key))
                return;

            // We have it either installed, available or in the cluster already -> skip
            // We could match versions and add missing ones, but that would be overkill for now
            var packageInfo = packageInfos.FirstOrDefault(p => p.Name == localPackage.Key);
            if (packageInfo is not null)
                continue;

            packageInfo = new ClusterPackageInfo
            {
                Name = localPackage.Key,
                Versions = localPackage.Value.Select(v => v.ToString()).ToList().AsReadOnly(),
            };
            packageInfos.Add(packageInfo);

            logger.LogDebug("Add local package {Package} with versions: {Versions}", packageInfo.Name, string.Join(",", packageInfo.Versions));
        }
    }

    private static void UpdateLatestVersion(List<ClusterPackageInfo> packageInfos)
    {
        // Installed version is always the highest installed one
        foreach (var packageInfo in packageInfos)
        {
            packageInfo.LatestUpdateVersion = packageInfo.GetHighestAvailableVersion();
        }
    }
}
