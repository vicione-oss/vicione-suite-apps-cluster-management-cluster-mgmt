using System.IO.Abstractions;
using System.IO.Compression;
using ClusterManagement.Backend.Designs.Consumers;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using ViciOne.Cluster.Model;
using ViciOne.Core.Dataflow.DataModel;
using ViciOne.Core.Dataflow.DataModel.Generation;
using ViciOne.Core.Dataflow.Pooling;
using ViciOne.Engine.DefaultPoolings;
using ViciOne.TreeBuilder.Rules;

namespace ClusterManagement.Backend.Designs;

internal sealed partial class InMemoryPackagesStore(
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IFileSystem fileSystem,
    IClusterDependencyStore dependencyStore,
    ILogger<InMemoryPackagesStore> logger) : IPackagesStore
{
    private readonly Dictionary<ClusterDependency, Content> _packages = [];
    private readonly Lock _lock = new();

    private PackagesLoader? _packagesLoader;

    private record Content(IReadOnlyDictionary<Guid, FunctionBlockDesign> Designs, IReadOnlyDictionary<string, Ruleset> Rulesets, bool Hide);

    /// <summary>
    /// Creates and initializes the packages loader.
    /// </summary>
    internal void Initialize(IReadOnlyCollection<ClusterDependency> dependencies)
        => _packagesLoader = PackagesLoader.Create(dependencies, fileSystem.GetDependenciesPath(workspaceProvider), fileSystem);

    public bool TryAddPackage(ClusterDependency dependency, string directory, bool hide = false)
    {
        if (_packagesLoader is null)
            throw new InvalidOperationException("Packages loader is not initialized.");

        lock (_lock)
        {
            if (_packages.ContainsKey(dependency))
                return false;

            var designs = _packagesLoader.Load(directory);
            var rulesets = RulesetLoader.LoadRulesets(directory);

            _packages[dependency] = new(designs, rulesets, hide);
            return true;
        }
    }

    public bool TryAddPackage(ClusterDependency dependency, IReadOnlyCollection<Type> types, IReadOnlyCollection<Ruleset> rulesets, bool hide = false)
    {
        lock (_lock)
        {
            if (_packages.ContainsKey(dependency))
                return false;

            PoolingFactory poolingFactory = new([typeof(SumPooling).Assembly]);

            List<FunctionBlockDesign> functionBlockDesigns = [];

            foreach (var type in types)
            {
                var functionBlockDesignInfo = RuntimeFunctionBlockAssemblyAnalyzer.GetFunctionBlockDesign(type);
                var coreDesign = FunctionBlockDesignGenerator.GenerateFunctionBlockDesign(functionBlockDesignInfo, poolingFactory);

                functionBlockDesigns.Add(coreDesign);
            }

            _packages[dependency] = new(
                functionBlockDesigns.ToDictionary(d => d.Id),
                rulesets.Where(r => r.Root is not null).ToDictionary(r => r.Root!.Id),
                hide);
            return true;
        }
    }

    public async Task<ClusterDependency> AddPackageManually(byte[] zipArchive, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream(zipArchive);
        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        var depsJsonEntry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith("deps.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The provided package archive does not contain a deps.json file.");

        // We could add validation of core version etc.
        var packageName = depsJsonEntry.Name.Replace(".deps.json", "", StringComparison.OrdinalIgnoreCase);
        var packageVersion = await GetPackageVersion(depsJsonEntry, packageName, cancellationToken);

        var dependenciesPath = fileSystem.GetDependenciesPath(workspaceProvider);
        var packageDestinationPath = fileSystem.Path.Combine(dependenciesPath, packageName, packageVersion);

        // Package already exists we assume it's not a productive system
        if (fileSystem.Directory.Exists(packageDestinationPath))
        {
            // What if the package is still in use by engine host?
            // EH probably is able to continue with previous version loaded into memory
            // If the FB is not loaded yet, but requested afterwards by LoadContext it will be a problem
            logger?.LogWarning("Removing existing FunctionBlock '{PackageName}' path '{Name}'", packageName, packageDestinationPath);
            fileSystem.Directory.Delete(packageDestinationPath, true);
        }

        // Extract package to our packages folder. It won't be loaded from there until we add it to our dependency store.
        fileSystem.Directory.CreateDirectory(packageDestinationPath);
        archive.ExtractToDirectory(packageDestinationPath);

        var clusterDependency = new ClusterDependency
        {
            Name = packageName,
            Version = new Version(packageVersion)
        };

        var operation = new ClusterDependencyOperation(clusterDependency, ClusterDependencyOperationKind.Install);

        // Queue the package for update on next restart
        await dependencyStore.EnqueueOperations([operation], cancellationToken);

        // Now a restart is required to actually load the package
        return clusterDependency;
    }

    private static async Task<string> GetPackageVersion(ZipArchiveEntry depsJsonEntry, string packageName, CancellationToken cancellationToken)
    {
        using var depsJsonStream = depsJsonEntry.Open();
        using var reader = new StreamReader(depsJsonStream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (!line.Contains(packageName, StringComparison.Ordinal))
                continue;

            if (PackageDepsJsonRegex.Parse(line, out var version))
                return version;
        }

        throw new InvalidOperationException($"Package version of '{packageName}' not found in deps.json");
    }

    public async Task DeletePackage(ClusterDependency dependency, CancellationToken cancellationToken)
    {
        // Check if we already have loaded the package
        bool inUse;
        lock (_lock)
        {
            inUse = _packages.ContainsKey(dependency);
        }

        // Is it even possible to delete a package that is not in use?
        if (!inUse)
        {
            var dependenciesPath = fileSystem.GetDependenciesPath(workspaceProvider);
            var packageDestinationPath = fileSystem.Path.Combine(dependenciesPath, dependency.Name, dependency.Version.ToString());

            if (fileSystem.Directory.Exists(packageDestinationPath))
            {
                logger?.LogInformation("Deleting package '{PackageName}' with path '{Name}'", dependency.Name, packageDestinationPath);
                fileSystem.Directory.Delete(packageDestinationPath, true);
            }
        }

        // Queue the package for uninstall on next restart - it might have been uploaded but not used yet
        var operation = new ClusterDependencyOperation(dependency, ClusterDependencyOperationKind.Uninstall);

        await dependencyStore.EnqueueOperations([operation], CancellationToken.None);
    }
}
