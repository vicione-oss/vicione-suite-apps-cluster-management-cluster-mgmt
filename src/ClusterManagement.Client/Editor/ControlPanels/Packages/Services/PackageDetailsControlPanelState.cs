using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using Sdk.Client.ControlPanels.Services;
using Sdk.Messaging;

namespace ClusterManagement.Client.Editor.ControlPanels.Packages.Services;

public sealed class PackageDetailsControlPanelState : ControlPanelState
{
    public ClusterPackageInfo? PackageInfo { get; set; }
    public string VersionToInstall { get; set; } = string.Empty;

    public bool UpdatePackage(ClusterDependencyChange change)
    {
        if (PackageInfo is null)
            return false;

        if (change.Action is CrudAction.Deleted)
        {
            PackageInfo.PendingOperation = null;
            VersionToInstall = string.Empty;
            return true;
        }

        if (change.Operation.OperationKind is ClusterDependencyOperationKind.Install)
        {
            VersionToInstall = change.Operation.Dependency.Version.ToString();
            PackageInfo.PendingOperation = change.Operation;
            return true;
        }

        if (change.Operation.OperationKind is ClusterDependencyOperationKind.Uninstall)
        {
            VersionToInstall = string.Empty;
            PackageInfo.PendingOperation = change.Operation;
            return true;
        }

        return false;
    }
}
