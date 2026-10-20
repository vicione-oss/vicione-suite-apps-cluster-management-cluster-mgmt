using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace ClusterManagement.Internal;

public sealed class ClusterManagementOptions
{
    [Range(1, int.MaxValue)]
    public int MqttRpcTimeout { get; set; } = 10;

    /// <summary>
    /// optional key to use a special set of packages like for edge-s
    /// </summary>
    public string? PackageSet { get; set; }

    /// <summary>
    /// optional - version of the engine host that should be downloaded and used
    /// </summary>
    public string? EngineHostVersion { get; set; }

    /// <summary>
    /// Allows hiding of the NavTile for the cluster editor
    /// </summary>
    public bool HideClusterEditorNavTile { get; set; }

    /// <summary>
    /// Per-state timeouts of the deploy cluster saga
    /// </summary>
    [ValidateObjectMembers]
    public DeploySagaTimeoutOptions DeploySagaTimeouts { get; set; } = new();
}
