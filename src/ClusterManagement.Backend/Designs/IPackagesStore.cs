using ViciOne.Cluster.Model;
using ViciOne.TreeBuilder.Rules;

namespace ClusterManagement.Backend.Designs;

public interface IPackagesStore
{
    Task<ClusterDependency> AddPackageManually(byte[] zipArchive, CancellationToken cancellationToken);
    
    Task DeletePackage(ClusterDependency dependency, CancellationToken cancellationToken);

    bool TryAddPackage(ClusterDependency dependency, string directory, bool hide = false);

    bool TryAddPackage(ClusterDependency dependency, IReadOnlyCollection<Type> types, IReadOnlyCollection<Ruleset> rulesets, bool hide = false);
}
