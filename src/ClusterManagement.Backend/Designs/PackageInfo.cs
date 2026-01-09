using Semver;

namespace ClusterManagement.Backend.Designs;

internal sealed record PackageInfo(string Name, SemVersion Version);
