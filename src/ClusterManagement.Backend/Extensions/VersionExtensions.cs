using ClusterManagement.Public.Enums;

namespace ClusterManagement.Backend.Extensions;

internal static class VersionExtensions
{
    public static Version GetVersionIncrement(this Version version, VersionIncrementType updateType) => updateType switch
    {
        VersionIncrementType.Major => new Version(version.Major + 1, 0, 0),
        VersionIncrementType.Minor => new Version(version.Major, version.Minor + 1, 0),
        VersionIncrementType.Patch => new Version(version.Major, version.Minor, version.Build + 1),
        _ => throw new InvalidOperationException($"{updateType} is no valid {nameof(VersionIncrementType)}"),
    };
}
