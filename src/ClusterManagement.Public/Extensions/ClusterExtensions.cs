using Sdk.Extensions;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Public.Extensions;

public static class ClusterExtensions
{
    /// <summary>
    /// When given the RootContainer returns all containers of the dataflow
    /// </summary>
    /// <param name="rootContainer"></param>
    /// <returns></returns>
    public static IEnumerable<Container> Flatten(this Container rootContainer)
        => rootContainer.Flatten(c => c.Containers);

    public static IEnumerable<(DataPortTreeNode Item, IReadOnlyCollection<DataPortTreeNode> Path)> Traverse(this DataPort dataPort)
        => dataPort.TreeNodes.SelectMany(n => n.TraverseDepthFirstWithPath(c => c.Children));

    public static IEnumerable<DataPortTreeNode> Flatten(this DataPortTreeNode dataPortTreeNode)
        => dataPortTreeNode.Flatten(c => c.Children);

    /// <summary>
    /// returns a flat list of all engine hosts in the cluster
    /// </summary>
    /// <param name="cluster"></param>
    /// <returns></returns>
    public static List<EngineHost> FindAllEngineHosts(this Cluster cluster)
        => [.. cluster.NodeGroups
            .SelectMany(ng => ng.Nodes)
            .SelectMany(n => n.Applications)
            .SelectMany(a => a.EngineHosts)];

    public static bool IsCoreOs(this ClusterApplication app)
        => app.Type is ClusterApplicationType.CoreOsMaster
            or ClusterApplicationType.CoreOsSlave
            or ClusterApplicationType.CoreOsStandalone;
}
