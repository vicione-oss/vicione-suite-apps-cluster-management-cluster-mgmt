using ClusterManagement.Internal.Contracts;

namespace ClusterManagement.Client.Editor.Extensions;

internal static class ClusterPackageInfoExtensions
{
    internal static string? GetHighestAvailableVersion(this ClusterPackageInfo packageInfo, string? selectedVersion = null)
    {
        if (packageInfo?.Versions is null || packageInfo.Versions.Count == 0)
            return null;

        Version? currentVersion = null;

        if (!string.IsNullOrWhiteSpace(selectedVersion) && Version.TryParse(selectedVersion, out var selectedParsed))
        {
            currentVersion = selectedParsed;
        }
        else if (Version.TryParse(packageInfo.InstalledVersion, out var installedParsed))
        {
            currentVersion = installedParsed;
        }

        if (currentVersion is null)
            return null;

        foreach (var v in packageInfo.Versions)
        {
            if (!Version.TryParse(v, out var candidate))
                continue;

            if (candidate > currentVersion)
                return v;
        }

        return null;
    }
}
