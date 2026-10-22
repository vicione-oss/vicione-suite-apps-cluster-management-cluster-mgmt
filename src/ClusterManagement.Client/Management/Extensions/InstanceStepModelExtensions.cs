using System.Globalization;
using ClusterManagement.Client.Management.Localization;
using ClusterManagement.Client.Management.Models;
using ClusterManagement.Public.Enums;

namespace ClusterManagement.Client.Management.Extensions;

internal static class InstanceStepModelExtensions
{
    public static string ToText(this InstanceStepModel step)
    {
        var format = step.Step switch
        {
            ClusterNodeDeploymentStep.Prepare => ClusterOverview.StepPrepare,
            ClusterNodeDeploymentStep.Plan => ClusterOverview.StepPlan,
            ClusterNodeDeploymentStep.CompareEngineHostSettings => ClusterOverview.StepCompareEngineHostSettings,
            ClusterNodeDeploymentStep.QueryEngineStates => ClusterOverview.StepQueryEngineStates,
            ClusterNodeDeploymentStep.DeployEngineHost => ClusterOverview.StepDeployEngineHost,
            ClusterNodeDeploymentStep.StartEngineHost => ClusterOverview.StepStartEngineHost,
            ClusterNodeDeploymentStep.StopEngineHost => ClusterOverview.StepStopEngineHost,
            ClusterNodeDeploymentStep.UpdateEngineHost => ClusterOverview.StepUpdateEngineHost,
            ClusterNodeDeploymentStep.TearDownEngineHost => ClusterOverview.StepTearDownEngineHost,
            ClusterNodeDeploymentStep.StopEngine => ClusterOverview.StepStopEngine,
            ClusterNodeDeploymentStep.TearDownEngine => ClusterOverview.StepTearDownEngine,
            ClusterNodeDeploymentStep.DeployEngine => ClusterOverview.StepDeployEngine,
            ClusterNodeDeploymentStep.StartEngine => ClusterOverview.StepStartEngine,
            ClusterNodeDeploymentStep.DeleteRetainedMessages => ClusterOverview.StepDeleteRetainedMessages,
            _ => step.Step.ToString(),
        };

        return string.Format(CultureInfo.CurrentCulture, format, step.Target ?? ClusterOverview.StepUnknownTarget);
    }

    /// <summary>
    /// What the instance is busy with during the step, in a few words that stay the same for a whole kind of steps.
    /// </summary>
    public static string ToPhaseText(this InstanceStepModel step) => step.Step switch
    {
        ClusterNodeDeploymentStep.Prepare or ClusterNodeDeploymentStep.Plan => ClusterOverview.BusyPreparing,
        ClusterNodeDeploymentStep.CompareEngineHostSettings or ClusterNodeDeploymentStep.QueryEngineStates => ClusterOverview.BusyCheckingEngineHosts,
        ClusterNodeDeploymentStep.DeployEngineHost or ClusterNodeDeploymentStep.StartEngineHost or ClusterNodeDeploymentStep.StopEngineHost
            or ClusterNodeDeploymentStep.UpdateEngineHost => ClusterOverview.BusyUpdatingEngineHosts,
        ClusterNodeDeploymentStep.StopEngine => ClusterOverview.BusyStoppingEngines,
        ClusterNodeDeploymentStep.TearDownEngine or ClusterNodeDeploymentStep.TearDownEngineHost => ClusterOverview.BusyRemovingEngines,
        ClusterNodeDeploymentStep.DeployEngine => ClusterOverview.BusyDeployingEngines,
        ClusterNodeDeploymentStep.StartEngine => ClusterOverview.BusyStartingEngines,
        _ => ClusterOverview.BusyCleaningUp,
    };
}
