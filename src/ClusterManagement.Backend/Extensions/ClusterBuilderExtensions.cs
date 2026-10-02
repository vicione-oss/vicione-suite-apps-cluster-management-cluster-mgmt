using ClusterManagement.Internal.Designs;
using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Builder.Extensions;

namespace ClusterManagement.Backend.Extensions;

internal static class ClusterBuilderExtensions
{
    public static void RemoveUnusedDependencies(this ClusterBuilder builder, ISuiteRulesetProvider rulesetProvider)
    {
        // reduce dependencies to what is really used in the cluster
        var functionBlockDesignIds = builder.Cache.GetUsedFunctionBlockDesignIds().ToArray();

        // DataPort.DesignId is e.g. MqttClientInstance
        var dataPortDependencies = builder.Cache.DataPorts
            .Select(k => rulesetProvider.GetClusterDependency(k.RulesetId))
            .Union([rulesetProvider.GetSystemDataPortDependency()])
            .Distinct()
            .ToArray();

        // join resolved FunctionBlock with the DataPort dependencies
        var usedDependencies = functionBlockDesignIds
            .Select(builder.DependencyResolver.ResolveFunctionBlockDesignDependency)
            .Union(dataPortDependencies)
            .ToArray();

        builder.Cluster.Dependencies.RemoveAll(k => !usedDependencies.Contains(k));
    }
}
