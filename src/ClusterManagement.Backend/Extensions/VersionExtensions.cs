using ClusterManagement.Public.Enums;

namespace ClusterManagement.Backend.Extensions;

internal static class VersionExtensions
{
    public static Version GetVersionIncrement(this Version version, VersionIncrementKind incrementKind) => incrementKind switch
    {
        VersionIncrementKind.Major => new Version(version.Major + 1, 0, 0),
        VersionIncrementKind.Minor => new Version(version.Major, version.Minor + 1, 0),
        VersionIncrementKind.Patch => new Version(version.Major, version.Minor, version.Build + 1),
        _ => throw new InvalidOperationException($"{incrementKind} is no valid {nameof(VersionIncrementKind)}"),
    };
}
