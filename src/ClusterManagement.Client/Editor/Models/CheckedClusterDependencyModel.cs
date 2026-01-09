using ViciOne.Cluster.Model;

namespace ClusterManagement.Client.Editor.Models;

internal class CheckedClusterDependencyModel(bool? checkValue, ClusterDependency? clusterDependency)
{
    public bool? Checked { get; set; } = checkValue;
    public ClusterDependency? ClusterDependency { get; set; } = clusterDependency;
}
