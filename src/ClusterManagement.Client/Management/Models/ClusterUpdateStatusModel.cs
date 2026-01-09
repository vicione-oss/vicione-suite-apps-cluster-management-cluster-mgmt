using ClusterManagement.Public.Events;

namespace ClusterManagement.Client.Management.Models;

public sealed class ClusterUpdateStatusModel
{
    private readonly ClusterUpdateStarted _start;

    public Guid ClusterId => _start.ClusterId;
    public Version Version => _start.TargetVersion;
    public List<ClusterUpdateNodeModel> Nodes { get; private set; } = [];

    public ClusterUpdateStatusModel(ClusterUpdateStarted start)
    {
        _start = start;

        foreach (var item in start.NodeApplications)
        {
            ClusterUpdateApplicationModel? app = null;
            if (item.Value is not null)
            {
                app = new ClusterUpdateApplicationModel
                {
                    Id = item.Value.Value,
                    Name = start.Applications[item.Value.Value],
                };
            }

            var node = new ClusterUpdateNodeModel()
            {
                Id = item.Key,
                Name = start.Nodes[item.Key],
                CoreOS = app,
                NeedCleanup = _start.NodesToClean.Contains(item.Key),
                NeedCreation = _start.NodesToCreate.Contains(item.Key),
                Processed = !_start.NodesToCreate.Contains(item.Key) && !_start.NodesToClean.Contains(item.Key),
            };

            Nodes.Add(node);
        }
    }
}
