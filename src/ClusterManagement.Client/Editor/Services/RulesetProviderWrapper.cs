using ClusterManagement.Internal.Designs;
using ViciOne.Cluster.Model;
using ViciOne.TreeBuilder.Rules;
using ViciOne.Ui.ClusterEditor.Services;

namespace ClusterManagement.Client.Editor.Services;

public class RulesetProviderWrapper(ISuiteRulesetProvider rulesetProvider) : IRulesetProvider
{
    public ClusterDependency GetClusterDependency(string dataPortType)
        => rulesetProvider.GetClusterDependency(dataPortType);

    public Ruleset GetRuleset(RulesetIdentifier rulesetIdentifier)
        => rulesetProvider.GetRuleset(rulesetIdentifier.Key);

    public IEnumerable<RulesetIdentifier> GetRulesetIdentifiers(string category)
        => rulesetProvider.GetAllRulesets().Select(k => new RulesetIdentifier(category, k.Root!.Id)); // root is not null is ensured on load

    public ClusterDependency GetSystemDataPortDependency()
        => rulesetProvider.GetSystemDataPortDependency();
}
