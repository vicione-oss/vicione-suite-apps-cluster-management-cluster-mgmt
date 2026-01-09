using ClusterManagement.Public.Requests;

namespace ClusterManagement.Client.Editor.Models;

/// <summary>
/// wrapper to group Cluster by Id with a list of available versions
/// </summary>
public class ClusterInfoVersionsModel(ClusterInfo latest, List<Version> versions)
{
    public ClusterInfo Latest { get; init; } = latest;
    public List<Version> Versions { get; } = versions;
}
