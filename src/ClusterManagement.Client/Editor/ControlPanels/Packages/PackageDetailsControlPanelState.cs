using ClusterManagement.Internal.Contracts;
using Sdk.Client.ControlPanels.Services;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages;

public sealed class PackageDetailsControlPanelState : ControlPanelState
{
    public ClusterPackageInfo? PackageInfo { get; set; }
    public string VersionToInstall { get; set; } = string.Empty;
}
