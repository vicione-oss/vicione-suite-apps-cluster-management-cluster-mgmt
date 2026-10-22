using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Management.Models;

public sealed record EngineHostRuntimeModel(Guid Id, Guid InstanceId, string Name, bool ElevatedPrivileges, IReadOnlyList<EngineRuntimeModel> Engines)
{
    public string? Description { get; init; }

    /// <summary>
    /// The cycle time in milliseconds all engines of the host share: the engine host runs them one after another in one
    /// cycle and rejects an engine whose cycle time differs from the one it runs with. Zero without engines.
    /// </summary>
    public uint CycleTime => Engines.Count == 0 ? 0 : Engines[0].MinCycleTime;

    /// <summary>
    /// Lists every engine host the deployment deploys with its engines, each tied to the instance that runs the host;
    /// like the deployment, only the nodes of the top-level node groups count.
    /// </summary>
    public static IReadOnlyList<EngineHostRuntimeModel> FromCluster(Cluster cluster)
        => [.. cluster.NodeGroups
            .SelectMany(group => group.Nodes)
            .SelectMany(node => node.Applications)
            .SelectMany(instance => instance.EngineHosts.Select(host => new EngineHostRuntimeModel(
                host.Id,
                instance.Id,
                host.Name,
                host.ElevatedPrivileges,
                [.. host.Engines.Select(engine => new EngineRuntimeModel(instance.Id, engine.Id, engine.Name, engine.MinCycleTime)
                {
                    Description = engine.Description,
                })])
            {
                Description = host.Description,
            }))];

    /// <summary>
    /// The names of the instances that run the engine hosts of <paramref name="cluster"/>, by instance id.
    /// </summary>
    public static Dictionary<Guid, string> InstanceNamesOf(Cluster cluster)
        => cluster.NodeGroups
            .SelectMany(group => group.Nodes)
            .SelectMany(node => node.Applications)
            .GroupBy(instance => instance.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

    /// <summary>
    /// Replaces the engine with the id of <paramref name="engine"/> in whichever host holds it.
    /// </summary>
    public static IReadOnlyList<EngineHostRuntimeModel> WithEngine(IReadOnlyList<EngineHostRuntimeModel> hosts, EngineRuntimeModel engine)
        => [.. hosts.Select(host => host with
        {
            Engines = [.. host.Engines.Select(e => e.EngineId == engine.EngineId ? engine : e)],
        })];
}
