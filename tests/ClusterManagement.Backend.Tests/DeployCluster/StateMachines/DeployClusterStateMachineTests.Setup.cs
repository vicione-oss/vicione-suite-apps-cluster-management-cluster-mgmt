using ClusterManagement.Public.Extensions;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Tests.DeployCluster.StateMachines;

public partial class DeployClusterStateMachineTests
{
    private static ClusterNodeGroup AddNodeGroup(Cluster cluster, Guid groupId)
    {
        var group = new ClusterNodeGroup
        {
            Id = groupId
        };
        cluster.NodeGroups.Add(group);
        return group;
    }

    private static void AddClusterApplication(ClusterNodeGroup group, Guid nodeId, Guid applicationId, ClusterApplicationType type) => group.Nodes.Add(new ClusterNode
    {
        Id = nodeId,
        Applications =
        {
            new ClusterApplication
            {
                Id = applicationId,
                Type = type,
            },
        },
    });

    private class DeployClusterStateMachineOptions
    {
        private readonly Dictionary<Guid, Guid?> _activeNodes;
        private readonly Dictionary<Guid, Guid?> _targetNodes;

        public Cluster ActiveCluster { get; private set; }
        public Cluster TargetCluster { get; private set; }
        public Version ActiveVersion => ActiveCluster.Version;

        public Version TargetVersion => TargetCluster.Version;

        public Guid ClusterId => Equals(ActiveCluster.Id, TargetCluster.Id)
            ? ActiveCluster.Id
            : throw new InvalidOperationException("cluster id mismatch");

        public Dictionary<Guid, Guid?> ClusterNodesToAddAndAppsToStart =>
            _targetNodes
            .Except(_activeNodes)
            .ToDictionary(x => x.Key, x => x.Value);

        public IEnumerable<Guid> ClusterNodeDeploymentsUpdates =>
            _targetNodes.Values
            .Where(v => v is not null).Select(v => v!.Value)
            .Union(_activeNodes.Values.Where(v => v is not null)
                .Select(v => v!.Value));

        public IEnumerable<Guid> ClusterNodesToClean => _activeNodes.Keys.Except(_targetNodes.Keys);

        public DeployClusterStateMachineOptions(Cluster activeCluster, Cluster targetCluster)
        {
            ActiveCluster = activeCluster;
            TargetCluster = targetCluster;

            _activeNodes = GetNodeApplicationPairs(ActiveCluster);
            _targetNodes = GetNodeApplicationPairs(TargetCluster);
        }

        private static Dictionary<Guid, Guid?> GetNodeApplicationPairs(Cluster cluster)
        => cluster.NodeGroups
            .SelectMany(ng => ng.Nodes)
            .ToDictionary(n => n.Id,
                n => n.Applications.FirstOrDefault(a => a.IsCoreOs())?.Id);
    }
}
