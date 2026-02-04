using ClusterManagement.Internal.Contracts;
using Semver;

namespace ClusterManagement.Backend.Designs;

public interface IPackageInfoProvider
{
    Task<List<ClusterPackageInfo>> GetClusterPackageInfos(CancellationToken cancellationToken, List<string>? filterPackages = null);

    SemVersion GetCoreVersion();
}
