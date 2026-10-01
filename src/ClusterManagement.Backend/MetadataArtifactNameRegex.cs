using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Semver;

namespace ClusterManagement.Backend;

internal static partial class MetadataArtifactNameRegex
{
    private const string VersionGroupName = "version";
    private const string RuntimeGroupName = "runtime";
    private const string CoreVersionGroupName = "coreversion";

    [GeneratedRegex(@$"^(?'{VersionGroupName}'{SemVersionPattern.WithoutBuildMetadata})-(?'{RuntimeGroupName}'(?:win|linux)-(?:x64|arm64))_(?'{CoreVersionGroupName}'\d+\.\d+\.\d+)\.json$", RegexOptions.CultureInvariant)]

    private static partial Regex Matcher();

    /// <summary>
    /// Splits a metadata artifact name of the form <c>{version}-{runtime}_{coreVersion}.json</c>.
    /// </summary>
    /// <param name="name">e.g. 0.28.0-linux-x64_0.48.0.json or 2.0.0-beta.1-ci2821027829-win-x64_1.0.0.json</param>
    /// <param name="packageVersion">e.g. 0.28.0, 0.28.0-ci234343, 2.0.0-beta.1 or 2.0.0-beta.1-ci2821027829</param>
    /// <param name="coreVersion">e.g. 0.48.0</param>
    /// <param name="runtime">e.g. linux-x64</param>
    /// <returns></returns>
    public static bool Parse(string? name, [NotNullWhen(true)] out string? packageVersion, [NotNullWhen(true)] out string? runtime, [NotNullWhen(true)] out string? coreVersion)
    {
        packageVersion = runtime = coreVersion = null;

        if (string.IsNullOrEmpty(name))
            return false;

        var match = Matcher().Match(name);
        if (!match.Success)
            return false;

        packageVersion = match.Groups[VersionGroupName].Value;
        runtime = match.Groups[RuntimeGroupName].Value;
        coreVersion = match.Groups[CoreVersionGroupName].Value;
        return true;
    }

    public static bool ParseVersion(string? name, [NotNullWhen(true)] out SemVersion? packageVersion)
    {
        packageVersion = null;

        if (!Parse(name, out var versionString, out _, out _))
            return false;

        return SemVersion.TryParse(versionString, out packageVersion);
    }
}
