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

        // A prerelease is only suggested to someone already on one; it can still be installed on purpose from the version list.
        var versions = new List<SemVersion>();
        foreach (var version in packageInfo.Versions)
        {
            if (SemVersion.TryParse(version.Version, SemVersionStyles.Any, out var parsed)
                && (parsed.IsRelease || installedParsed.IsPrerelease))
            {
                versions.Add(parsed);
            }
        }

        var maxVersion = versions.Max(SemVersion.SortOrderComparer);

        return maxVersion?.ComparePrecedenceTo(installedParsed) > 0 ? maxVersion.ToString() : null;
    }
}
