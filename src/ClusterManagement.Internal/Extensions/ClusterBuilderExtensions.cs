using ViciOne.Cluster.Builder;
using ViciOne.Cluster.Builder.Extensions;
using ViciOne.Cluster.Model;
using ViciOne.Core.Contracts;

namespace ClusterManagement.Internal.Extensions;

public static class ClusterBuilderExtensions
{
    public static EngineHost GetOrCreateEngineHostByName(this ClusterBuilder builder, ClusterApplication application, string engineHostName)
    {
        var eh = application.EngineHosts.FirstOrDefault(h => h.Name == engineHostName);
        if (eh is not null)
            return eh;

        eh = builder.Editors.Application.AddEngineHost(application, engineHostName);
        builder.Editors.EngineHost.SetDescription(eh, $"Engine host for app '{application.Id}'");

        return eh;
    }

    public static Engine GetOrCreateEngineByName(this ClusterBuilder builder, EngineHost engineHost, string engineName, string? description)
    {
        var engine = engineHost.Engines.FirstOrDefault(k => k.Name == engineName);
        if (engine is not null)
            return engine;

        engine = builder.Editors.EngineHost.AddEngine(engineHost, engineName);

        if (!string.IsNullOrEmpty(description))
            builder.Editors.Engine.SetDescription(engine, description);

        return engine;
    }

    public static void EnsureDependencyExists(this ClusterBuilder builder, ClusterDependency dependency)
    {
        if (!builder.Cache.IsDependencyExisting(dependency))
            builder.Editors.Cluster.AddDependency(dependency.Name, dependency.Version);
    }

    public static Type? ResolveConnectorDesignSourceType(this ClusterBuilder builder, Connector? connector)
    {
        if (connector is null)
            return null;

        var connectorDesign = builder.ResolveConnectorDesign(connector);
        return DataTypeCompatibilityValidator.DetermineValueType(connectorDesign.ConnectorType);
    }

    public static IEnumerable<DataPortTreeNode> GetAllNestedDataPortTreeNodes(this Cluster cluster)
    {
        foreach (var dataPort in cluster.Dataflows.SelectMany(k => k.DataPorts))
        {
            foreach (var childNode in dataPort.TreeNodes)
            {
                yield return childNode;
                foreach (var allNestedContainer in childNode.GetAllNestedDataPortTreeNodes())
                {
                    yield return allNestedContainer;
                }
            }
        }
    }

    private static IEnumerable<DataPortTreeNode> GetAllNestedDataPortTreeNodes(this DataPortTreeNode treeNode)
    {
        foreach (var childNode in treeNode.Children)
        {
            yield return childNode;
            foreach (var allNestedContainer in childNode.GetAllNestedDataPortTreeNodes())
            {
                yield return allNestedContainer;
            }
        }
    }
}
