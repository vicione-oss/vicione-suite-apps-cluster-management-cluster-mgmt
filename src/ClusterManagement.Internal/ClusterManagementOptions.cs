using System.ComponentModel.DataAnnotations;

namespace ClusterManagement.Internal;

public sealed class ClusterManagementOptions
{
    private string? _packageSet;

    [Range(1, int.MaxValue)]
    public int MqttRpcTimeout { get; set; } = 10;

    [Obsolete($"This property will be removed in future releases. Use `{nameof(PackageSet)}` property instead.")]
    /// <summary>
    /// temporary re-added to support env parameters set by image version <= 1.0.2
    /// Will get removed by image 1.0.3 and replaced by <see cref="PackageSet"/>
    /// </summary>
    public string? FunctionBlockSet { get; set; }

    /// <summary>
    /// optional key to use a special set of packages like for edge-s
    /// </summary>
    public string? PackageSet
    {
        get => !string.IsNullOrEmpty(_packageSet) ? _packageSet : FunctionBlockSet;
        set => _packageSet = value;
    }

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
