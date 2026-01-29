using System.IO.Abstractions;
using ClusterManagement.Backend.Designs.Extensions;
using Sdk.Backend.Modules;
using ViciOne.Cluster.Model;
using ViciOne.Core.Dataflow.DataModel;
using ViciOne.Core.Dataflow.DataModel.Generation;
using ViciOne.Core.Dataflow.Pooling;
using ViciOne.Engine.DefaultPoolings;
using ViciOne.TreeBuilder.Rules;

namespace ClusterManagement.Backend.Designs;

internal class PackageContentProvider(
    IClusterDependencyStore dependencyStore,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IFileSystem fileSystem) : IPackageContentProvider
{
    private readonly Dictionary<ClusterDependency, PackageContent> _packages = [];
    private readonly Lock _lock = new();

    private PackageLoader? _packagesLoader;

    public PackageContent GetPackageContent(ClusterDependency dependency)
        => _packages.TryGetValue(dependency, out var content)
            ? content
            : throw new KeyNotFoundException($"Package content for dependency '{dependency}' not found.");

    public IReadOnlyDictionary<ClusterDependency, PackageContent> GetPackageContents()
        => _packages;

    /// <summary>
    /// Creates and initializes the packages loader.
    /// </summary>
    private async Task<PackageLoader> CreatePackageLoader(CancellationToken stoppingToken)
    {
        var dependencies = await dependencyStore.LoadDependencies(stoppingToken);
        return PackageLoader.Create(dependencies, fileSystem.GetDependenciesPath(workspaceProvider), fileSystem);
    }

    public async Task<bool> TryAddPackage(ClusterDependency dependency, string directory, bool hide = false, CancellationToken cancellationToken = default)
    {
        _packagesLoader ??= await CreatePackageLoader(cancellationToken);

        lock (_lock)
        {
            if (_packages.ContainsKey(dependency))
                return false;

            try
            {
                var designs = _packagesLoader.Load(directory);
                var rulesets = RulesetLoader.LoadRulesets(directory);

                if (designs.Count == 0 && rulesets.Count == 0)
                    throw new InvalidOperationException($"No valid designs or rulesets were found in the provided package '{dependency.Name}' version '{dependency.Version}'.");

                _packages[dependency] = new(designs, rulesets, hide);
                return true;
            }
            catch (Exception ex)
            {
                _packages[dependency] = new PackageContent(new Dictionary<Guid, FunctionBlockDesign>(), new Dictionary<string, Ruleset>(), true) { Error = ex };
            }
        }

        return false;
    }

    public async Task<bool> TryAddPackage(ClusterDependency dependency, IReadOnlyCollection<Type> types, IReadOnlyCollection<Ruleset> rulesets, bool hide = false, CancellationToken cancellationToken = default)
    {
        _packagesLoader ??= await CreatePackageLoader(cancellationToken);

        lock (_lock)
        {
            if (_packages.ContainsKey(dependency))
                return false;

            try
            {
                PoolingFactory poolingFactory = new([typeof(SumPooling).Assembly]);

                List<FunctionBlockDesign> functionBlockDesigns = [];

                foreach (var type in types)
                {
                    var functionBlockDesignInfo = RuntimeFunctionBlockAssemblyAnalyzer.GetFunctionBlockDesign(type);
                    var coreDesign = FunctionBlockDesignGenerator.GenerateFunctionBlockDesign(functionBlockDesignInfo, poolingFactory);

                    functionBlockDesigns.Add(coreDesign);
                }

                if (functionBlockDesigns.Count == 0 && rulesets.Count == 0)
                    throw new InvalidOperationException($"No valid designs or rulesets were found in the provided package '{dependency.Name}' version '{dependency.Version}'.");

                _packages[dependency] = new(
                    functionBlockDesigns.ToDictionary(d => d.Id),
                    rulesets.Where(r => r.Root is not null).ToDictionary(r => r.Root!.Id),
                    hide);

                return true;
            }
            catch (Exception ex)
            {
                _packages[dependency] = new PackageContent(new Dictionary<Guid, FunctionBlockDesign>(), new Dictionary<string, Ruleset>(), true) { Error = ex };
            }
        }

        return false;
    }
}
