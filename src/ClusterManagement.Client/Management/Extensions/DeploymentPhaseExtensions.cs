using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;

namespace ClusterManagement.Client.Management.Extensions;

internal static class DeploymentPhaseExtensions
{
    public static string ToText(this DeploymentPhase phase) => phase switch
    {
        DeploymentPhase.Committing => ClusterOverview.PhaseCommitting,
        DeploymentPhase.AddingNodes => ClusterOverview.PhaseAddingNodes,
        DeploymentPhase.UpdatingDeployments => ClusterOverview.PhaseUpdatingDeployments,
        DeploymentPhase.CleaningUp => ClusterOverview.PhaseCleaningUp,
        DeploymentPhase.RollingBack => ClusterOverview.PhaseRollingBack,
        DeploymentPhase.Completed => ClusterOverview.PhaseCompleted,
        DeploymentPhase.Failed => ClusterOverview.PhaseFailed,
        _ => ClusterOverview.PhaseStarting,
    };
}
