using ViciOne.Cluster.Model;
using ViciOne.TreeBuilder.Rules;

namespace ClusterManagement.Internal.Designs;

public interface ISuiteRulesetProvider
{
    Ruleset GetRuleset(string category, string rulesetId);
    IEnumerable<Ruleset> GetAllRulesets();
    ClusterDependency GetClusterDependency(string dataPortType);
    ClusterDependency GetSystemDataPortDependency();
}
