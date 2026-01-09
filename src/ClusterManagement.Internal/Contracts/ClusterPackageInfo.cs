namespace ClusterManagement.Internal.Contracts;

public class ClusterPackageInfo
{
    public required string Name { get; set; }

    public required string MinCoreVersion { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Contains the version that is already installed on the system
    /// </summary>
    public string? InstalledVersion { get; set; }

    /// <summary>
    /// All available versions for this package
    /// </summary>
    public IReadOnlyCollection<string>? Versions { get; set; }
}
