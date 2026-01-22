using System.Diagnostics;
using System.IO.Abstractions;
using System.Reflection;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Public;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using Semver;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Designs;

internal sealed class DesignLoader(
    IFileSystem fileSystem,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IClusterDependencyStore clusterDependencyStore,
    IPackagesStore packagesStore,
    IPackageArtifactRepository repository,
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

            if (packagesStore is InMemoryPackagesStore inMemoryPackagesStore)
                inMemoryPackagesStore.Initialize(dependencies);

            // ViciOne.Suite.System.DataPort is a FunctionBlock interface for the providers
            var systemDataPortDependency = LoadSystemDataPortFunctionBlock();

            var existingPackages = _fileSystem.GetComponents(packagesPath);

            foreach (var dependency in dependencies)
            {
                if (dependency.Name.Equals(systemDataPortDependency.Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                // We only load packages that exists as dependency (actually packages.json)
                var existingPackage = existingPackages.FirstOrDefault(p => p.Key.Name == dependency.Name && ToVersion(p.Key.Version) == dependency.Version);
                if (existingPackage.Key is null)
                    throw new InvalidOperationException($"Expected package '{dependency.Name}' version '{dependency.Version}' can't be found.");

                if (!packagesStore.TryAddPackage(dependency, existingPackage.Value))
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

    private ClusterDependency LoadSystemDataPortFunctionBlock()
    {
        var dataportType = typeof(ViciOne.Suite.System.DataPort);
        var systemDataPortDependency = CreateDependency(dataportType);

        if (!packagesStore.TryAddPackage(systemDataPortDependency, [dataportType], [], true))
            _logger.LogWarning("Failed to add System DataPort package {Name} {Version} to store.", systemDataPortDependency.Name, systemDataPortDependency.Version);

        if (packagesStore is InMemoryPackagesStore inMemoryPackagesStore)
            inMemoryPackagesStore.SetSystemDataPortDependency(systemDataPortDependency);

        return systemDataPortDependency;
    }

    private static ClusterDependency CreateDependency(Type type)
        => CreateDependency(type.Assembly);

    private static ClusterDependency CreateDependency(Assembly assembly)
    {
        var assemblyName = assembly.GetName();
        var assemblyVersion = assemblyName.Version ?? throw new InvalidDataException();

        // assembly version might be 0.1.0.0 but our version has major.minor.build
        return new()
        {
            Name = assemblyName.Name!,
            Version = new(assemblyVersion.Major, assemblyVersion.Minor, assemblyVersion.Build),
        };
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

    private static Version ToVersion(SemVersion version)
        => version.IsPrerelease
            ? new Version((int)version.Major, (int)version.Minor, (int)version.Patch)
            : version.ToVersion();
}
