using System.Diagnostics;
using System.IO.Abstractions;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Public;
using ClusterManagement.Public.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using Sdk.Messaging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Designs;

internal sealed partial class DesignLoader(
    IFileSystem fileSystem,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IClusterDependencyStore clusterDependencyStore,
    IPackageContentProvider packageContentProvider,
    IPackageArtifactRepository repository,
    ISuiteRulesetProvider rulesetProvider,
    ILogger<DesignLoader> logger) : BackgroundService, IDownloader
{
    private readonly TaskCompletionSource _downloadProcess = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal int LoadedDataPortDesigns { get; private set; }
    internal int LoadedFunctionBlockDesigns { get; private set; }

    public Task Task => _downloadProcess.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => _downloadProcess.TrySetCanceled(stoppingToken));
        try
        {
            var packagesPath = fileSystem.GetDependenciesPath(workspaceProvider);
            fileSystem.Directory.CreateDirectory(packagesPath);

            var dependencies = await clusterDependencyStore.LoadDependencies(stoppingToken);
            var failedDownloads = await DownloadClusterDependencies(dependencies, stoppingToken);

            foreach (var dependency in failedDownloads)
            {
                LogDownloadFailure(logger, dependency.Name, dependency.Version);

                packageContentProvider.AddFailedPackage(dependency,
                    new ErrorInfo(ClusterError.PackageFailedToLoad, $"Package '{dependency.Name}' version '{dependency.Version}' could not be downloaded."));
            }

            // ViciOne.Suite.System.DataPort is a FunctionBlock interface for the providers
            var systemDataPortDependency = await rulesetProvider.AddSystemDataPortFunctionBlock(stoppingToken);

            var existingPackages = fileSystem.GetComponents(packagesPath);

            foreach (var dependency in dependencies)
            {
                if (dependency.Name.Equals(systemDataPortDependency.Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (failedDownloads.Contains(dependency))
                    continue;

                // We only load packages that exists as dependency (actually packages.json)
                var existingPackage = existingPackages.FirstOrDefault(p => p.Key.Name == dependency.Name && p.Key.Version.ToString() == dependency.Version);
                if (existingPackage.Key is null)
                {
                    LogDependencyNotFound(logger, dependency.Name, dependency.Version);
                    packageContentProvider.AddFailedPackage(dependency,
                        new ErrorInfo(ClusterError.PackageFailedToLoad, $"Expected package '{dependency.Name}' version '{dependency.Version}' can't be found on disk."));
                    continue;
                }

                if (!await packageContentProvider.TryAddPackage(dependency, existingPackage.Value, false, stoppingToken))
                    LogAddPackageFailure(logger, dependency.Name, dependency.Version);
            }

            ClusterSerializer.SetTypedSerializerOptions();
        }
        catch (Exception ex)
        {
            _downloadProcess.TrySetException(ex);
        }
        finally
        {
            _downloadProcess.TrySetResult();
        }
    }

    private async Task<HashSet<ClusterDependency>> DownloadClusterDependencies(IReadOnlyCollection<ClusterDependency> dependencies, CancellationToken stoppingToken)
    {
        var watch = Stopwatch.StartNew();
        var downloadTasks = dependencies.Select(async fb => (Dependency: fb, Result: await repository.DownloadAndExtract(fb.Name, fb.Version, stoppingToken)));
        var results = await Task.WhenAll(downloadTasks);

        watch.Stop();

        LogDownloadStatistics(
            logger,
            results.Count(r => r.Result is { Skipped: false, Error: null }),
            results.Count(r => r.Result.Skipped),
            results.Count(r => r.Result.Error is not null),
            watch.Elapsed);

        HashSet<ClusterDependency> failed = [];
        foreach (var (dependency, result) in results.Where(k => k.Result.Error is not null))
        {
            LogUpdateClusterDependencyError(logger, result.Error, dependency.Name, dependency.Version, result.SourcePath);
            failed.Add(dependency);
        }

        return failed;
    }

    [LoggerMessage(LogLevel.Warning, "Failed to download cluster dependency {Name} {Version}.")]
    private static partial void LogDownloadFailure(ILogger<DesignLoader> logger, string name, string version);

    [LoggerMessage(LogLevel.Warning, "Package {Name} {Version} is listed as a dependency but was not found on disk.")]
    private static partial void LogDependencyNotFound(ILogger<DesignLoader> logger, string name, string version);

    [LoggerMessage(LogLevel.Warning, "Failed to add package {Name} {Version} to store.")]
    private static partial void LogAddPackageFailure(ILogger<DesignLoader> logger, string name, string version);

    [LoggerMessage(LogLevel.Information, "Downloading {Count} cluster dependencies (skipped={Skipped}, errors={Errors}) took {Elapsed}")]
    private static partial void LogDownloadStatistics(ILogger<DesignLoader> logger, int count, int skipped, int errors, TimeSpan elapsed);

    [LoggerMessage(LogLevel.Error, "Failed to update cluster dependency {Name} {Version} from {Source}")]
    private static partial void LogUpdateClusterDependencyError(ILogger<DesignLoader> logger, Exception? exception, string name, string version, string source);
}
