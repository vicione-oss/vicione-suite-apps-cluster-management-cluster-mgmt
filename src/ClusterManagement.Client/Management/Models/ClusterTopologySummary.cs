using ClusterManagement.Public.Extensions;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Management.Models;

/// <summary>
/// Counts of a cluster version. <c>Datapoints</c> are the DataPort tree nodes that carry a value, linked or not;
/// <c>LinkedDatapoints</c> are those linked to a function block in at least one direction, so they are transferred.
/// </summary>
public sealed record ClusterTopologySummary(
    int Nodes,
    int Instances,
    int EngineHosts,
    int Engines,
    int Dataflows,
    int FunctionBlocks,
    int DataPorts,
    int Datapoints,
    int LinkedDatapoints)
{
    public static ClusterTopologySummary Create(Cluster cluster)
    {
        // Like the deployment, which deploys only the nodes of the top-level node groups.
        var nodes = cluster.NodeGroups.SelectMany(group => group.Nodes).ToList();
        var engineHosts = cluster.FindAllEngineHosts();
        var functionBlocks = cluster.Dataflows.SelectMany(d => d.Root.Flatten()).SelectMany(c => c.FunctionBlocks).ToList();
        var dataPorts = cluster.Dataflows.SelectMany(d => d.DataPorts).ToList();
        var datapoints = dataPorts.SelectMany(p => p.TreeNodes).SelectMany(n => n.Flatten()).Where(n => n.ValueType is not null).ToList();
        var readByFunctionBlocks = ReadByFunctionBlocks(functionBlocks);

        return new ClusterTopologySummary(
            Nodes: nodes.Count,
            Instances: nodes.SelectMany(n => n.Applications).Count(a => a.IsCoreOs()),
            EngineHosts: engineHosts.Count,
            Engines: engineHosts.Sum(h => h.Engines.Count),
            Dataflows: cluster.Dataflows.Count,
            FunctionBlocks: functionBlocks.Count,
            DataPorts: dataPorts.Count,
            Datapoints: datapoints.Count,
            LinkedDatapoints: datapoints.Count(n => n.IncomingLinks.Count > 0 || readByFunctionBlocks.Contains(n.Id)));
    }

    // A link is stored at its destination: a datapoint written by a function block holds it in IncomingLinks,
    // a datapoint read by a function block is the source of a link held by the function block's input.
    private static HashSet<Guid> ReadByFunctionBlocks(IEnumerable<FunctionBlock> functionBlocks)
        => [.. functionBlocks
            .SelectMany(fb => fb.Inputs.Concat(fb.Settings.Select(s => s.Input)))
            .SelectMany(input => input.IncomingLinks)
            .Select(link => link.SourceDataPortTreeNodeId)
            .OfType<Guid>()];
}
