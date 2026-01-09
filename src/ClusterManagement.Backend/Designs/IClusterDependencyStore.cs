using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Designs;

internal interface IClusterDependencyStore
{
    Task<IReadOnlyCollection<ClusterDependency>> LoadDependencies(CancellationToken cancellationToken);
}
