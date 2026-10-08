namespace ClusterManagement.Client.Management.Models;

public enum ClusterVersionStatus
{
    Running,
    Deploying,
    Failed,
    Draft,
    Default,
    Previous,
}

internal static class ClusterVersionStatuses
{
    public static ClusterVersionStatus Of(ClusterInfoModel version, DeploymentProgressModel? deployment)
    {
        if (deployment?.Matches(version.Id, version.Version) == true)
        {
            if (deployment.IsRunning)
                return ClusterVersionStatus.Deploying;

            if (deployment.Phase == DeploymentPhase.Failed)
                return ClusterVersionStatus.Failed;
        }

        if (version.IsActive)
            return ClusterVersionStatus.Running;

        if (version.IsLatest)
            return ClusterVersionStatus.Draft;

        return version.IsDefault ? ClusterVersionStatus.Default : ClusterVersionStatus.Previous;
    }
}
