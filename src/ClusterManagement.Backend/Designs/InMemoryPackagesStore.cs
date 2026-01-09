using System.IO.Abstractions;
using ClusterManagement.Backend.Designs.Extensions;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using ViciOne.Cluster.Model;
using ViciOne.Core.Dataflow.DataModel;
using ViciOne.Core.Dataflow.DataModel.Generation;
using ViciOne.Core.Dataflow.Pooling;
using ViciOne.Engine.DefaultPoolings;
using ViciOne.TreeBuilder.Rules;

namespace ClusterManagement.Backend.Designs;

public sealed partial class InMemoryPackagesStore(
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IFileSystem fileSystem,
    ILogger<InMemoryPackagesStore> logger) : IPackagesStore
{
    private readonly Dictionary<ClusterDependency, Content> _packages = [];
    private readonly Lock _lock = new();

    private PackagesLoader? _packagesLoader;

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

    private record Content(IReadOnlyDictionary<Guid, FunctionBlockDesign> Designs, IReadOnlyDictionary<string, Ruleset> Rulesets, bool Hide);
}
