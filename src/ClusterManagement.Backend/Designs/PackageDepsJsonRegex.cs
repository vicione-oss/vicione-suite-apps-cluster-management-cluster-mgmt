using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Semver;

namespace ClusterManagement.Backend.Designs.Consumers;

internal static partial class PackageDepsJsonRegex
{
    private const string VersionGroupName = "version";

    [GeneratedRegex(@$"\""[\w.]+\/(?'{VersionGroupName}'\d+\.\d+\.\d+[-ci\d]*)\""", RegexOptions.CultureInvariant)]

    private static partial Regex Matcher();

    /// <summary>
    /// https://regex101.com/r/lyytBD/1
    /// </summary>
    /// <param name="line">e.g. "ACX.FunctionBlocks.ConstantDouble/0.27.0": { from deps.json</param>
    /// <param name="packageVersion">Parsed version part e.g. 0.27.0</param>        
    public static bool Parse(string? line, [NotNullWhen(true)] out string? packageVersion)
    {
        packageVersion = null;

        if (string.IsNullOrEmpty(line))
            return false;

        var match = Matcher().Match(line);
        if (!match.Success)
            return false;

        packageVersion = match.Groups[VersionGroupName].Value;
        return true;
    }

    public static bool ParseVersion(string? line, [NotNullWhen(true)] out SemVersion? packageVersion)
    {
        packageVersion = null;

        if (!Parse(line, out var version))
            return false;

        return SemVersion.TryParse(version, out packageVersion);
    }
}
