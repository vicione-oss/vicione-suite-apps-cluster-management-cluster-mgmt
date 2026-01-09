using System.Runtime.InteropServices;
using ClusterManagement.Internal;
using Semver;

namespace ClusterManagement.Backend.Extensions;

internal static class ClusterManagementOptionsExtensions
{
    private static readonly SemVersion s_engineHostVersion = new(0, 28, 0);

    internal static SemVersion GetEngineHostVersion(this ClusterManagementOptions options)
    {
        var version = s_engineHostVersion;
        if (SemVersion.TryParse(options.EngineHostVersion, SemVersionStyles.Strict, out var configuredVersion) &&
            configuredVersion.CompareSortOrderTo(version) > 0)
        {
            version = configuredVersion;
        }
        return version;
    }

    public static string GetEngineHostPackageName(this ClusterManagementOptions options)
    {
        var version = options.GetEngineHostVersion();
        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" : "linux";
#pragma warning disable CA1308
        // filename is lowercase
        return $"{version}-{platform}-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}";
#pragma warning restore CA1308
    }
}
