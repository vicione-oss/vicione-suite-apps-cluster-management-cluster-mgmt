using System.Diagnostics;
using System.IO.Abstractions;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Internal.Designs;
using ClusterManagement.Public;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Designs;

internal sealed class DesignLoader(
    IFileSystem fileSystem,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IClusterDependencyStore clusterDependencyStore,
    IPackageContentProvider packageContentProvider,
    IPackageArtifactRepository repository,
    ISuiteRulesetProvider rulesetProvider,
    ILogger<DesignLoader> logger) : BackgroundService, IDownloader
{
    private readonly IWorkspaceProvider<ClusterManagementBackendModule> _workspaceProvider = workspaceProvider;
    private readonly IClusterDependencyStore _clusterDependencyStore = clusterDependencyStore;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IPackageArtifactRepository _repository = repository;
    private readonly ILogger<DesignLoader> _logger = logger;
    private readonly TaskCompletionSource _downloadProcess = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal int LoadedDataPortDesigns { get; private set; }
    internal int LoadedFunctionBlockDesigns { get; private set; }

    public Task Task => _downloadProcess.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => _downloadProcess.TrySetCanceled(stoppingToken));
        try
        {
            var packagesPath = _fileSystem.GetDependenciesPath(_workspaceProvider);
            _fileSystem.Directory.CreateDirectory(packagesPath);

            var dependencies = await _clusterDependencyStore.LoadDependencies(stoppingToken);
            await DownloadClusterDependencies(dependencies, stoppingToken);

            // ViciOne.Suite.System.DataPort is a FunctionBlock interface for the providers
            var systemDataPortDependency = await rulesetProvider.AddSystemDataPortFunctionBlock(stoppingToken);

            var existingPackages = _fileSystem.GetComponents(packagesPath);

            foreach (var dependency in dependencies)
            {
                if (dependency.Name.Equals(systemDataPortDependency.Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                // We only load packages that exists as dependency (actually packages.json)
                var existingPackage = existingPackages.FirstOrDefault(p => p.Key.Name == dependency.Name && p.Key.Version.ToString() == dependency.Version);
                if (existingPackage.Key is null)
                    throw new InvalidOperationException($"Expected package '{dependency.Name}' version '{dependency.Version}' can't be found.");

                if (!await packageContentProvider.TryAddPackage(dependency, existingPackage.Value, false, stoppingToken))
                    _logger.LogWarning("Failed to add package {Name} {Version} to store.", dependency.Name, dependency.Version);
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

    private async Task DownloadClusterDependencies(IReadOnlyCollection<ClusterDependency> dependencies, CancellationToken stoppingToken)
    {
        var watch = Stopwatch.StartNew();
        var results = await Task.WhenAll(dependencies.Select(fb => _repository.DownloadAndExtract(fb.Name, fb.Version, stoppingToken)));

        watch.Stop();

        _logger.LogInformation("Downloading {Count} cluster dependencies (skipped={Skipped}, errors={Errors}) took {Elapsed}",
            results.Count(r => r is { Skipped: false, Error: null }),
            results.Count(r => r.Skipped),
            results.Count(r => r.Error is not null),
            watch.Elapsed);

        foreach (var result in results.Where(k => k.Error is not null))
            _logger.LogError(result.Error, "Failed to update cluster dependency {Source}", result.SourcePath);
    }
}
