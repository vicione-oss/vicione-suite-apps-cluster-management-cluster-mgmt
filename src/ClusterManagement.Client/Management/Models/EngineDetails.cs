using ClusterManagement.Public.Extensions;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Management.Models;

/// <summary>
/// What a cluster version says about one of its engines beside its state.
/// </summary>
internal sealed record EngineDetails(uint RunIndex, bool Enabled, string LogLevel, int FunctionBlocks)
{
    /// <summary>
    /// An engine the cluster version does not know runs last.
    /// </summary>
    public static EngineDetails Unknown { get; } = new(uint.MaxValue, true, string.Empty, 0);

    /// <summary>
    /// The details of every engine of <paramref name="cluster"/>, by engine id.
    /// </summary>
    public static Dictionary<Guid, EngineDetails> Of(Cluster cluster)
    {
        // Function blocks belong to the engine that runs them, wherever their dataflow places them.
        var functionBlocks = cluster.Dataflows
            .SelectMany(dataflow => dataflow.Root.Flatten())
            .SelectMany(container => container.FunctionBlocks)
            .Where(block => block.EngineId is not null)
            .GroupBy(block => block.EngineId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        return cluster.NodeGroups
            .SelectMany(group => group.Nodes)
            .SelectMany(node => node.Applications)
            .SelectMany(instance => instance.EngineHosts)
            .SelectMany(host => host.Engines)
            .GroupBy(engine => engine.Id)
            .ToDictionary(group => group.Key, group =>
            {
                var engine = group.First();
                return new EngineDetails(engine.RunIndex, engine.Enabled, engine.LogLevel.ToString(), functionBlocks.GetValueOrDefault(engine.Id));
            });
    }
}
