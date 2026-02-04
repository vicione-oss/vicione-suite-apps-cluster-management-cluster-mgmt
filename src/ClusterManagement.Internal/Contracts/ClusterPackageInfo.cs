using System.Diagnostics;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Contracts;

[DebuggerDisplay("{Name} installed:{InstalledVersion}, cluster-ref:{RunningClusterVersion} update: {LatestUpdateVersion}")]
public class ClusterPackageInfo
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Contains the version that is already installed on the system otherwise <see langword="null"/>
    /// </summary>
    public string? InstalledVersion { get; set; }

    public string? AvailableUpdateVersion { get; set; }

    /// <summary>
    /// All available versions for this package if any otherwise <see langword="null"/>
    /// </summary>
    public IReadOnlyCollection<ClusterPackageVersion>? Versions { get; set; }

    /// <summary>
    /// Contains the pending operation for this package if any otherwise <see langword="null"/>
    /// </summary>
    public ClusterDependencyOperation? PendingOperation { get; set; }

    /// <summary>
    /// Filled if the package is part of the running cluster with the version deployed there
    /// If installed version does not match we have a deployment issue! User should know about
    /// </summary>
    public string? RunningClusterVersion { get; set; }

    /// <summary>
    /// Is not <see langword="null"/> if there occured an error on e.g. loading the package
    /// </summary>
    public ErrorInfo? Error { get; set; }
}

public record ClusterPackageVersion(string Version)
{
    public bool IsLocal { get; set; }
}
