using Sdk.Extensions;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Public.Extensions;

/// <summary>
/// Provides extension methods for navigating and querying cluster-related objects 
/// such as containers, data ports, and engine hosts.
/// </summary>
public static class ClusterExtensions
{
    /// <summary>
    /// Recursively flattens a container tree structure into a flat collection.
    /// </summary>
    /// <param name="rootContainer">The root container to start from.</param>
    /// <returns>All containers in the hierarchy including the root container.</returns>
    public static IEnumerable<Container> Flatten(this Container rootContainer)
        => rootContainer.Flatten(c => c.Containers);

    /// <summary>
    /// Recursively flattens a <see cref="DataPortTreeNode"/> and its children into a flat list.
    /// </summary>
    /// <param name="dataPortTreeNode">The root tree node to flatten.</param>
    /// <returns>All nodes in the hierarchy including the root node.</returns>
    public static IEnumerable<DataPortTreeNode> Flatten(this DataPortTreeNode dataPortTreeNode)
        => dataPortTreeNode.Flatten(c => c.Children);

    /// <summary>
    /// Traverses the <see cref="DataPort"/> tree using a depth-first strategy,
    /// returning each node along with the path from the root.
    /// </summary>
    /// <param name="dataPort">The data port whose tree to traverse.</param>
    /// <returns>A collection of nodes and their respective paths from the root.</returns>
    public static IEnumerable<(DataPortTreeNode Item, IReadOnlyCollection<DataPortTreeNode> Path)> Traverse(this DataPort dataPort)
        => dataPort.TreeNodes.SelectMany(n => n.TraverseDepthFirstWithPath(c => c.Children));

    /// <summary>
    /// Retrieves a flat list of all <see cref="EngineHost"/> instances within the given cluster.
    /// </summary>
    /// <param name="cluster">The cluster to search.</param>
    public static List<EngineHost> FindAllEngineHosts(this Cluster cluster)
        => [.. cluster.NodeGroups
            .SelectMany(ng => ng.Nodes)
            .SelectMany(n => n.Applications)
            .SelectMany(a => a.EngineHosts)];

    /// <summary>
    /// Determines whether the given application is a CoreOS-type application.
    /// </summary>
    /// <param name="app">The application to inspect.</param>
    public static bool IsCoreOs(this ClusterApplication app)
        => app.Type is ClusterApplicationType.CoreOsMaster
            or ClusterApplicationType.CoreOsSlave
            or ClusterApplicationType.CoreOsStandalone;
}
