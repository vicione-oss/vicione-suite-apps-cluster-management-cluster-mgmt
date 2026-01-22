using ClusterManagement.Internal.Designs;
using ViciOne.Cluster.Model;
using ViciOne.TreeBuilder.Rules;

namespace ClusterManagement.Backend.Designs;

internal sealed partial class InMemoryPackagesStore : ISuiteRulesetProvider
{
    private ClusterDependency? _systemDataPortDependency;

    public Ruleset GetRuleset(string category, string rulesetId)
    {
        foreach (var entry in _packages)
        {
            if (entry.Value.Rulesets.ContainsKey(rulesetId))
                return entry.Value.Rulesets[rulesetId];
        }
        throw new InvalidOperationException($"Can't find Ruleset for category:{category} and rulesetId:{rulesetId}");
    }

    public IEnumerable<Ruleset> GetAllRulesets()
        => _packages.Values.SelectMany(k => k.Rulesets.Values);

    public ClusterDependency GetClusterDependency(string dataPortType)
    {
        foreach (var entry in _packages)
        {
            if (entry.Value.Rulesets.ContainsKey(dataPortType))
                return entry.Key;
        }

        throw new InvalidOperationException($"Can't find ClusterDependency for DataPort type:{dataPortType}");
    }

    public void SetSystemDataPortDependency(ClusterDependency clusterDependency)
        => _systemDataPortDependency = clusterDependency;

    public ClusterDependency GetSystemDataPortDependency()
        => _systemDataPortDependency ?? throw new InvalidOperationException($"Use {nameof(SetSystemDataPortDependency)} before adding rulesets");
}
