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
}
