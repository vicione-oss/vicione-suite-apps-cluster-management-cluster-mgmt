using System.IO.Abstractions;
using ClusterManagement.Backend.Designs.Extensions;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using Sdk.Messaging;
using ViciOne.Cluster.Model;
using ViciOne.Core.Dataflow.DataModel;
using ViciOne.Core.Dataflow.DataModel.Generation;
using ViciOne.Core.Dataflow.Pooling;
using ViciOne.Engine.DefaultPoolings;
using ViciOne.TreeBuilder.Rules;

namespace ClusterManagement.Backend.Designs;

internal partial class PackageContentProvider(
    IClusterDependencyStore dependencyStore,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IFileSystem fileSystem,
    ILogger<PackageContentProvider> logger) : IPackageContentProvider
{
    private readonly Dictionary<ClusterDependency, PackageContent> _packages = [];
    private readonly Lock _lock = new();

    private PackageLoader? _packagesLoader;

    public PackageContent GetPackageContent(ClusterDependency dependency)
    {
        lock (_lock)
        {
            return _packages.TryGetValue(dependency, out var content)
                ? content
                : throw new KeyNotFoundException($"Package content for dependency '{dependency}' not found.");
        }
    }

    public IReadOnlyDictionary<ClusterDependency, PackageContent> GetPackageContents()
    {
        lock (_lock)
        {
            return _packages.ToDictionary();
        }
    }

    /// <summary>
    /// Creates and initializes the packages loader.
    /// </summary>
    private async Task<PackageLoader> CreatePackageLoader(CancellationToken stoppingToken)
    {
        var dependencies = await dependencyStore.LoadDependencies(stoppingToken);
        List<ClusterDependency> loadableDependencies;
        lock (_lock)
        {
            loadableDependencies = [.. dependencies.Where(d => !_packages.TryGetValue(d, out var pkg) || pkg.Error is null)];
        }
        return PackageLoader.Create(loadableDependencies, fileSystem.GetDependenciesPath(workspaceProvider), fileSystem);
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
                var loadResult = _packagesLoader.Load(directory);
                var rulesets = RulesetLoader.LoadRulesets(directory);

                if (!loadResult.ContainsViciOneContent && rulesets.Count == 0)
                    throw new InvalidOperationException($"Package '{dependency.Name}' version '{dependency.Version}' does not contain any ViciOne content (FunctionBlocks, Poolings, Validators or Rulesets) and cannot be loaded.");

                _packages[dependency] = new(loadResult.Designs, rulesets, hide);
                return true;
            }
            catch (Exception ex)
            {
                LogLoadPackageError(logger, ex, dependency.Name, dependency.Version, "directory");
                _packages[dependency] = new PackageContent(new Dictionary<Guid, FunctionBlockDesign>(), new Dictionary<string, Ruleset>(), true) { Error = new ErrorInfo(ex.HResult, ex.Message) };
            }
        }

        return false;
    }

    private static bool IsPoolingValidatorOrConversion(Type t)
        => typeof(ViciOne.Core.Contracts.DataModel.IAggregatingPooling).IsAssignableFrom(t) ||
        typeof(ViciOne.Core.Contracts.DataModel.IValidator).IsAssignableFrom(t) ||
        typeof(ViciOne.Core.Contracts.DataModel.ITypeConversion).IsAssignableFrom(t);

    public void AddFailedPackage(ClusterDependency dependency, ErrorInfo error)
    {
        lock (_lock)
        {
            _packages.TryAdd(dependency, new PackageContent(
                new Dictionary<Guid, FunctionBlockDesign>(),
                new Dictionary<string, Ruleset>(),
                true)
            { Error = error });
        }
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

                foreach (var type in types.Where(t => !IsPoolingValidatorOrConversion(t)))
                {
                    var functionBlockDesignInfo = RuntimeFunctionBlockAssemblyAnalyzer.GetFunctionBlockDesign(type);
                    var coreDesign = FunctionBlockDesignGenerator.GenerateFunctionBlockDesign(functionBlockDesignInfo, poolingFactory);

                    functionBlockDesigns.Add(coreDesign);
                }

                var containsViciOneContent = functionBlockDesigns.Count > 0 || types.Any(IsPoolingValidatorOrConversion);
                if (!containsViciOneContent && rulesets.Count == 0)
                    throw new InvalidOperationException($"Package '{dependency.Name}' version '{dependency.Version}' does not contain any ViciOne content (FunctionBlocks, Poolings, Validators or Rulesets) and cannot be loaded.");

                _packages[dependency] = new(
                    functionBlockDesigns.ToDictionary(d => d.Id),
                    rulesets.Where(r => r.Root is not null).ToDictionary(r => r.Root!.Id),
                    hide);

                return true;
            }
            catch (Exception ex)
            {
                LogLoadPackageError(logger, ex, dependency.Name, dependency.Version, "types");

                _packages[dependency] = new PackageContent(new Dictionary<Guid, FunctionBlockDesign>(), new Dictionary<string, Ruleset>(), true) { Error = new ErrorInfo(ex.HResult, ex.Message) };
            }
        }

        return false;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load package '{Name}' version '{Version}' from {Source}")]
    private static partial void LogLoadPackageError(ILogger<PackageContentProvider> logger, Exception exception, string name, string version, string source);
}
