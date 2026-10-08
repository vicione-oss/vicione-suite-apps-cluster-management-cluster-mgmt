using ClusterManagement.Client.Management.Models;
using ClusterManagement.Public.Requests;

namespace ClusterManagement.Client.Tests.Management;

/// <summary>
/// Versions of one cluster as the Cluster Manager lists them.
/// </summary>
internal static class ClusterInfoModels
{
    public static Guid ClusterId { get; } = Guid.NewGuid();

    public static ClusterInfoModel Version(string version, bool isActive = false, bool isLatest = false, DateTimeOffset? deployed = null)
        => new(new ClusterInfo(ClusterId, System.Version.Parse(version), "Cluster", isActive) { Deployed = deployed }) { IsLatest = isLatest };
}
