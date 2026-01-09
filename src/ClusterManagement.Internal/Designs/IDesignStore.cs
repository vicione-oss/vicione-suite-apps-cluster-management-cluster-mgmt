using ClusterManagement.Public.Designs;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Internal.Designs;

public interface IDesignStore : IDesignProvider
{
    string DesignDirectory { get; }

    void AddDataPort(ClusterDependency dependency, params string[] rootChildNodeId);
    Task AddDesign(ClusterDependency clusterDependency, Type type);
    Task<int> AddDesigns(ClusterDependency clusterDependency, Component component);
}
