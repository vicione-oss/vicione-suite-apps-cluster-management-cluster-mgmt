using ClusterManagement.Internal.Contracts;
using Semver;

namespace ClusterManagement.Backend.Designs.Extensions;

internal static class ClusterPackageInfoExtensions
{
    internal static string? GetHighestAvailableVersion(this ClusterPackageInfo packageInfo)
    {
        if (string.IsNullOrWhiteSpace(packageInfo.InstalledVersion))
            return null;

        if (packageInfo.Versions is null || packageInfo.Versions.Count == 0)
            return null;

        // Parse current version        
        if (!SemVersion.TryParse(packageInfo.InstalledVersion, SemVersionStyles.Any, out var installedParsed))
            return null;

        var versions = new List<SemVersion>();
        foreach (var version in packageInfo.Versions)
        {
            if (SemVersion.TryParse(version.Version, SemVersionStyles.Any, out var parsed))
                versions.Add(parsed);
        }

        var maxVersion = versions.Max(SemVersion.SortOrderComparer);

        return maxVersion != installedParsed ? maxVersion?.ToString() : null;
    }
}
