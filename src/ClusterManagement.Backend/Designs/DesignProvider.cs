using ClusterManagement.Public.Designs;
using Microsoft.Extensions.Logging;
using ViciOne.Cluster.Builder.Abstractions;
using ViciOne.Cluster.Model;
using ViciOne.Core.Dataflow.DataModel;

namespace ClusterManagement.Backend.Designs;

internal sealed partial class DesignProvider(IPackageContentProvider packageContentProvider, ILogger<DesignProvider> logger) : IDesignProvider
{
    public Task<IDependencyResolver> CreateResolver()
        => Task.FromResult<IDependencyResolver>(new ClusterDependencyResolver(GetDependencyPackages()));

    public Task<IReadOnlyCollection<ClusterDependency>> GetClusterDependencies(bool includeDataPortDependencies = false)
    {
        var packages = packageContentProvider.GetPackageContents();

        if (includeDataPortDependencies)
            return Task.FromResult<IReadOnlyCollection<ClusterDependency>>([.. packages.Keys]);

        return Task.FromResult<IReadOnlyCollection<ClusterDependency>>([.. packages.Where(p => !p.Value.Rulesets.Any()).Select(p => p.Key)]);
    }

    public Task<IReadOnlyCollection<Guid>> GetFunctionBlockDesignIds(params IReadOnlyCollection<ClusterDependency> clusterDependencies)
        => Task.FromResult<IReadOnlyCollection<Guid>>([.. GetFunctionBlockDesignsInternal(clusterDependencies).Select(d => d.Id)]);

    public Task<IReadOnlyCollection<FunctionBlockDesign>> GetFunctionBlockDesigns(params IReadOnlyCollection<ClusterDependency> clusterDependencies)
        => Task.FromResult(GetFunctionBlockDesignsInternal(clusterDependencies));

    private ClusterDependencyPackage[] GetDependencyPackages()
    {
        var packageContents = packageContentProvider.GetPackageContents();
        Dictionary<ClusterDependency, ClusterDependencyPackage> packages = new(packageContents.Count);

        foreach (var (dependency, storeItem) in packageContents)
        {
            if (packages.TryGetValue(dependency, out var package))
            {
                package.FunctionBlockDesigns.AddRange(storeItem.Designs.Values);
                package.DataPortRootChildNodeIds.AddRange(storeItem.Rulesets.Values
                    .Where(r => r.Root is not null)
                    .SelectMany(r => r.Root!.ChildNodes.Select(n => n.Id)));
            }
            else
            {
                packages[dependency] = new(dependency, [.. storeItem.Designs.Values], [..
                    storeItem.Rulesets.Values
                    .Where(r => r.Root is not null)
                    .SelectMany(r => r.Root!.ChildNodes.Select(n => n.Id))]);
            }
        }

        return [.. packages.Values];
    }

    private IReadOnlyCollection<FunctionBlockDesign> GetFunctionBlockDesignsInternal(params IReadOnlyCollection<ClusterDependency> clusterDependencies)
    {
        List<FunctionBlockDesign> requestedDesigns = [];

        foreach (var clusterDependency in clusterDependencies)
            requestedDesigns.AddRange(GetFunctionBlockDesigns(clusterDependency));

        return requestedDesigns;
    }

    private IEnumerable<FunctionBlockDesign> GetFunctionBlockDesigns(ClusterDependency clusterDependency)
    {
        var packageContents = packageContentProvider.GetPackageContents();
        if (packageContents.TryGetValue(clusterDependency, out var item))
        {
            if (item.Hide)
            {
                LogHiddenDependency(logger, clusterDependency.Name, clusterDependency.Version);
                yield break;
            }

            foreach (var design in item.Designs.Values)
                yield return design;
            yield break;
        }

        LogMissingDependency(logger, clusterDependency.Name, clusterDependency.Version);
    }

    [LoggerMessage(LogLevel.Debug, "Cluster dependency {Name} version {Version} is hidden.")]
    private static partial void LogHiddenDependency(ILogger<DesignProvider> logger, string name, string version);

    [LoggerMessage(LogLevel.Error, "Cluster dependency {Name} version {Version} can't be found.")]
    private static partial void LogMissingDependency(ILogger<DesignProvider> logger, string name, string version);
}
