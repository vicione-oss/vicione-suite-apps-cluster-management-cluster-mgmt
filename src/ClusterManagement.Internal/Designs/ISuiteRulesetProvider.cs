using ViciOne.Cluster.Model;
using ViciOne.TreeBuilder.Rules;

namespace ClusterManagement.Internal.Designs;

/// <summary>
/// Provides access to Suite rulesets and manages the system DataPort function block.
/// </summary>
public interface ISuiteRulesetProvider
{
    /// <summary>
    /// Adds the system DataPort function block package to the provider.
    /// </summary>    
    Task<ClusterDependency> AddSystemDataPortFunctionBlock(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all rulesets from all registered packages.
    /// </summary>    
    IEnumerable<Ruleset> GetAllRulesets();

    /// <summary>
    /// Gets a specific ruleset by category and ruleset identifier.
    /// </summary>    
    Ruleset GetRuleset(string category, string rulesetId);

    /// <summary>
    /// Gets the cluster dependency for a specific DataPort type.
    /// </summary>    
    ClusterDependency GetClusterDependency(string dataPortType);

    /// <summary>
    /// Gets the system DataPort dependency that was previously added.
    /// </summary>    
    ClusterDependency GetSystemDataPortDependency();
}
