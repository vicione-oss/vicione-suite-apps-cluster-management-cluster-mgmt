using ClusterManagement.Internal.Designs;
using Microsoft.Extensions.Logging;
using ViciOne.Cluster.Model;
using ViciOne.TreeBuilder.Rules;

namespace ClusterManagement.Backend.Designs;

internal sealed class RulesetProvider(IPackageContentProvider packageProvider, ILogger<RulesetProvider> logger) : ISuiteRulesetProvider
{
    private ClusterDependency? _systemDataPortDependency;

    public async Task<ClusterDependency> AddSystemDataPortFunctionBlock(CancellationToken cancellationToken = default)
    {
        var dataportType = typeof(ViciOne.Suite.System.DataPort);
        var systemDataPortDependency = ClusterDependencyFactory.CreateDependency(dataportType);

        if (!await packageProvider.TryAddPackage(systemDataPortDependency, [dataportType], [], true, cancellationToken))
            logger.LogWarning("Failed to add System DataPort package {Name} {Version} to store.", systemDataPortDependency.Name, systemDataPortDependency.Version);

        _systemDataPortDependency = systemDataPortDependency;

        return systemDataPortDependency;
    }

    public Ruleset GetRuleset(string category, string rulesetId)
    {
        foreach (var entry in packageProvider.GetPackageContents())
        {
            if (entry.Value.Rulesets.ContainsKey(rulesetId))
                return entry.Value.Rulesets[rulesetId];
        }
        throw new InvalidOperationException($"Can't find Ruleset for category:{category} and rulesetId:{rulesetId}");
    }

    public IEnumerable<Ruleset> GetAllRulesets()
        => packageProvider.GetPackageContents().SelectMany(k => k.Value.Rulesets.Values);

    public ClusterDependency GetClusterDependency(string dataPortType)
    {
        foreach (var entry in packageProvider.GetPackageContents())
        {
            if (entry.Value.Rulesets.ContainsKey(dataPortType))
                return entry.Key;
        }

        throw new InvalidOperationException($"Can't find ClusterDependency for DataPort type:{dataPortType}");
    }

    public ClusterDependency GetSystemDataPortDependency()
        => _systemDataPortDependency ?? throw new InvalidOperationException($"Use {nameof(AddSystemDataPortFunctionBlock)} before adding rulesets");
}
