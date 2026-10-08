using System.Globalization;
using ClusterManagement.Client.Extensions;
using ClusterManagement.Client.Management.Extensions;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using Microsoft.AspNetCore.Components;

namespace ClusterManagement.Client.Management.Components;

/// <summary>
/// Always shown, so the page does not move when a deployment starts: without a deployment in this session it shows
/// the running version as the result of the last one.
/// </summary>
public sealed partial class DeploymentPanel
{
    private bool _showAll;

    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)] private TimeProvider TimeProvider { get; set; } = default!;

    [Parameter] public DeploymentProgressModel? Deployment { get; set; }

    /// <summary>
    /// <c>null</c> while the first version of the cluster is being deployed.
    /// </summary>
    [Parameter] public ClusterInfoModel? Running { get; set; }

    /// <summary>
    /// The failed instances, or every instance once the user asked for all of them.
    /// </summary>
    internal IEnumerable<InstanceProgressModel> VisibleInstances
    {
        get
        {
            if (Deployment is null)
                return [];

            return _showAll ? Deployment.Instances : Deployment.Instances.Where(i => i.HasFailed);
        }
    }

    private string ShowInstancesText => (_showAll, Deployment?.Instances.Count) switch
    {
        (true, _) => ClusterOverview.ShowFewer,
        (false, 1) => ClusterOverview.ShowInstance,
        (false, var count) => Format(ClusterOverview.ShowAllInstances, count),
    };

    private IReadOnlyList<DeploymentStep> Steps => Deployment?.GetSteps() ?? DeploymentProgressModel.CompletedSteps;

    private string StartedText => Deployment?.StartedAt is { } startedAt
        ? Format(ClusterOverview.StartedAt, startedAt.To24HourTime(TimeProvider.LocalTimeZone))
        : string.Empty;

    private string Headline => Deployment?.Phase switch
    {
        null => Running is null ? ClusterOverview.NoActiveVersion : Format(ClusterOverview.RunningVersionDeployed, Running.Version),
        DeploymentPhase.Completed => Format(ClusterOverview.DeploymentCompleted, Deployment.TargetVersion, Deployment.Duration?.ToDisplayText(CultureInfo.CurrentCulture)),
        DeploymentPhase.Failed => Format(ClusterOverview.DeploymentFailed, Deployment.TargetVersion),
        _ => Format(ClusterOverview.DeployingVersion, Deployment.TargetVersion),
    };

    private string StatusLine
    {
        get
        {
            if (Deployment is null)
            {
                return Running?.Deployed is { } deployed
                    ? Format(ClusterOverview.NoDeploymentObserved, deployed.ToShortDateAnd24HourTime(TimeProvider.LocalTimeZone))
                    : string.Empty;
            }

            if (Deployment.Phase == DeploymentPhase.Failed)
                return FailureText(Deployment);

            return Format(ClusterOverview.InstancesUpdated, Deployment.Instances.Count(i => i.IsUpdated), Deployment.Instances.Count);
        }
    }

    private static string FailureText(DeploymentProgressModel deployment)
    {
        var failedIn = Format(ClusterOverview.FailedInPhase,
            PhaseText(deployment.FailurePhase ?? deployment.ForwardPhase),
            deployment.Duration?.ToDisplayText(CultureInfo.CurrentCulture));

        // Before the first phase no step is marked failed and no instance has an error, so only the reason explains it.
        if (deployment.ForwardPhase == DeploymentPhase.Starting && deployment.FailureReason is { } reason)
            return $"{failedIn}. {reason}";

        if (deployment.RollbackFailed)
            return $"{failedIn}. {ClusterOverview.RollbackFailed}";

        return deployment.HasRolledBack ? $"{failedIn}. {ClusterOverview.RolledBack}" : failedIn;
    }

    private static string Format(string format, params object?[] args)
        => string.Format(CultureInfo.CurrentCulture, format, args);

    private static string StepClass(DeploymentStepState state) => state switch
    {
        DeploymentStepState.Done => "done",
        DeploymentStepState.Failed => "failed",
        DeploymentStepState.Current => "current",
        _ => "pending",
    };

    private static string StepMark(DeploymentStepState state) => state switch
    {
        DeploymentStepState.Done => "✓",
        DeploymentStepState.Failed => "✕",
        DeploymentStepState.Current => "●",
        _ => "○",
    };

    private static string PhaseText(DeploymentPhase phase) => phase switch
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

    private static string InstanceStatusName(InstanceProgressModel instance) => instance switch
    {
        { HasFailed: true } => "failed",
        { IsUpdated: true } => "running",
        _ => "deploying",
    };

    private static string InstanceStatusText(InstanceProgressModel instance) => instance switch
    {
        { HasFailed: true } => ClusterOverview.InstanceFailed,
        { IsUpdated: true } => ClusterOverview.InstanceUpdated,
        { IsStarted: true } => ClusterOverview.InstanceStarted,
        _ => ClusterOverview.InstanceWaiting,
    };
}
