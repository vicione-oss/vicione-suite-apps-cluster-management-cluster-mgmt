using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Backend.Snapshots.Extensions;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Public.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Artifacts;
using Semver;

namespace ClusterManagement.Backend.Designs;

public partial class PackageInfoProvider(IClusterDependencyStore clusterDependencyStore,
    IClusterSnapshotStore clusterSnapshotStore,
    IClusterDbContext dbContext,
    IPackageContentProvider packageContentProvider,
    IPackageArtifactRepository repository,
    ILogger<PackageInfoProvider> logger) : IPackageInfoProvider
{
    public SemVersion GetCoreVersion() => SemVersion.FromVersion(ClusterAssembliesResolver.GetViciOneCoreAssemblyVersion());

    public async Task<List<ClusterPackageInfo>> GetClusterPackageInfos(CancellationToken cancellationToken, List<string>? filterPackages = null)
    {
        var packageInfos = new List<ClusterPackageInfo>();
        var coreVersion = GetCoreVersion();

        // Add packages we have already loaded or configured by packages.json
        await AddLoadedPackages(packageInfos, filterPackages, cancellationToken);

        // Add cluster packages available on jfrog for referenced core version
        await AddAvailablePackageVersions(packageInfos, filterPackages, coreVersion, cancellationToken);

        // Additionally there might be outstanding operations enqueued for next startup already
        await AddClusterPackageOperations(packageInfos, filterPackages, cancellationToken);

        // Add package versions from active cluster dependencies
        await AddActiveClusterDependencies(packageInfos, filterPackages, cancellationToken);

        // There might be manual uploaded packages that are not installed and not available        
        AddLocallyStoredPackages(packageInfos, filterPackages);

        // Provide information of available updates
        UpdateLatestVersion(packageInfos);

        // Sort package infos by name
        packageInfos.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        return packageInfos;
    }

    private async Task AddLoadedPackages(List<ClusterPackageInfo> packageInfos, List<string>? packagesOfInterest, CancellationToken cancellationToken)
    {
        // Using the loaded packages will provide additional error information if loading failed
        var installed = packageContentProvider.GetPackageContents()
            .Where(k => packagesOfInterest is null || packagesOfInterest.Contains(k.Key.Name))
            .Select(package => new ClusterPackageInfo
            {
                Name = package.Key.Name,
                InstalledVersion = package.Key.Version,
                Error = package.Value.Error,
            });

        // Loading the packages is done async in a background service - it might not be finished yet
        if (installed.Any())
        {
            packageInfos.AddRange(installed);
            return;
        }

        var fallback = await clusterDependencyStore.LoadDependencies(cancellationToken);
        var fallbackPackages = fallback
            .Where(k => packagesOfInterest is null || packagesOfInterest.Contains(k.Name))
            .Select(dependency => new ClusterPackageInfo
            {
                Name = dependency.Name,
                InstalledVersion = dependency.Version,
            });

        packageInfos.AddRange(fallbackPackages);
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
            var available = await repository.QueryMetadataArtifacts(packagesOfInterest, coreVersion, cancellationToken);

            UpdateAvailablePackageVersions(packageInfos, available);
        }
    }

    private static void UpdateAvailablePackageVersions(List<ClusterPackageInfo> packageInfos, IEnumerable<IArtifact> available)
    {
        // Create Dictionary<PackageName, IEnumerable<Version>>:
        var availablePackages = available
            .GroupBy(k => k.Path)
            .Select(g => (
                Name: Path.GetFileName(g.Key),
                Versions: g.Select(a => MetadataArtifactNameRegex.ParseVersion(a.Name, out var v)
                    ? v.ToString()
                    : null)
                    .Where(v => v != null)
                    .Distinct()
                    .ToList() // Materialize once
            ))
            .Where(p => p.Versions.Count > 0) // Filter empty
            .ToDictionary(p => p.Name, p => p.Versions);

        foreach (var packageName in availablePackages.Keys)
        {
            var versions = availablePackages[packageName]
                .Where(k => k != null)
                .Select(k => new ClusterPackageVersion(k!)).ToList().AsReadOnly();

            // Update versions with available ones
            var packageInfo = GetOrCreatePackage(packageInfos, packageName);
            packageInfo.Versions = versions;
        }
    }

    private async Task AddClusterPackageOperations(List<ClusterPackageInfo> packageInfos, List<string>? packagesOfInterest, CancellationToken cancellationToken)
    {
        var operations = (await clusterDependencyStore.GetEnqueuedOperations(cancellationToken))
            .Where(k => packagesOfInterest == null || packagesOfInterest.Contains(k.Dependency.Name))
            .ToList();

        foreach (var operation in operations)
        {
            var packageInfo = GetOrCreatePackage(packageInfos, operation.Dependency.Name);

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

                var packageInfo = GetOrCreatePackage(packageInfos, name);

                // Mark that this version is active in a running cluster
                packageInfo.RunningClusterVersion = SemVersion.Parse(version).ToString();

            }, cancellationToken);
        }
    }

    private void AddLocallyStoredPackages(List<ClusterPackageInfo> packageInfos, List<string>? packagesOfInterest)
    {
        var localPackages = clusterDependencyStore.BuildStoredPackageVersionMap();

        foreach (var localPackage in localPackages)
        {
            if (packagesOfInterest is not null && !packagesOfInterest.Contains(localPackage.Key))
                continue;

            // We have it either installed, available or in the cluster already -> skip
            // We could match versions and add missing ones, but that would be overkill for now
            var packageInfo = GetOrCreatePackage(packageInfos, localPackage.Key);
            var versionsToAdd = new List<ClusterPackageVersion>();

            foreach (var localVersion in localPackage.Value)
            {
                var packageVersion = packageInfo.Versions?.FirstOrDefault(k => k.Version == localVersion.ToString());
                if (packageVersion is not null)
                {
                    packageVersion.IsLocal = true;
                    continue;
                }

                versionsToAdd.Add(new ClusterPackageVersion(localVersion.ToString())
                {
                    IsLocal = true,
                });
            }

            if (versionsToAdd.Count > 0)
            {
                packageInfo.Versions = versionsToAdd
                    .Union(packageInfo.Versions ?? [])
                    .OrderByDescending(k => k.Version)
                    .ToList()
                    .AsReadOnly();

                LogPackageUpdate(logger, packageInfo.Name, string.Join(",", versionsToAdd));
            }
        }
    }

    private static ClusterPackageInfo GetOrCreatePackage(List<ClusterPackageInfo> packageInfos, string packageName)
    {
        var existing = packageInfos.FirstOrDefault(p => p.Name == packageName);
        if (existing is not null)
            return existing;

        var newPackage = new ClusterPackageInfo { Name = packageName };
        packageInfos.Add(newPackage);
        return newPackage;
    }

    private static void UpdateLatestVersion(List<ClusterPackageInfo> packageInfos)
    {
        // Installed version is always the highest installed one
        foreach (var packageInfo in packageInfos)
        {
            packageInfo.AvailableUpdateVersion = packageInfo.GetHighestAvailableVersion();
        }
    }

    [LoggerMessage(LogLevel.Warning, "Update package {Name} with local versions: {Versions}")]
    private static partial void LogPackageUpdate(ILogger<PackageInfoProvider> logger, string name, string versions);
}
