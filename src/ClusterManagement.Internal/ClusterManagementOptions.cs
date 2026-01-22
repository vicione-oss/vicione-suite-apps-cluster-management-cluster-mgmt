using System.ComponentModel.DataAnnotations;

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
    /// optional - if true, the packages file in the workspace will be overwritten with the embedded default one on startup
    /// </summary>
    public bool OverwritePackagesFiles { get; set; } = true;

    /// <summary>
    /// optional - version of the engine host that should be downloaded and used
    /// </summary>
    public string? EngineHostVersion { get; set; }

    /// <summary>
    /// Allows hiding of the NavTile for the cluster editor
    /// </summary>
    public bool HideClusterEditorNavTile { get; set; }
}
