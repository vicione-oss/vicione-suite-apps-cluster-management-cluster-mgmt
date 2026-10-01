namespace ClusterManagement.Backend;

internal static class SemVersionPattern
{
    private const string NumericIdentifier = @"(?:0|[1-9]\d*)";
    private const string PrereleaseIdentifier = $@"(?:{NumericIdentifier}|\d*[A-Za-z-][0-9A-Za-z-]*)";

    /// <summary>
    /// A SemVer 2.0.0 version with an optional prerelease, following the grammar on semver.org, e.g. 2.0.0, 2.0.0-ci2821027829 or 2.0.0-beta.1-ci2821027829. Build metadata is not matched.
    /// </summary>
    internal const string WithoutBuildMetadata = $@"{NumericIdentifier}\.{NumericIdentifier}\.{NumericIdentifier}(?:-{PrereleaseIdentifier}(?:\.{PrereleaseIdentifier})*)?";
}
