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
    /// <summary>
    /// The draft and the default are versions of the running cluster; another cluster's versions are only previous ones.
    /// Without <paramref name="runningClusterId"/> no cluster runs and every cluster has its draft and default.
    /// </summary>
    public static ClusterVersionStatus Of(ClusterInfoModel version, DeploymentProgressModel? deployment, Guid? runningClusterId = null)
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

        if (runningClusterId is { } running && version.Id != running)
            return ClusterVersionStatus.Previous;

        if (version.IsLatest)
            return ClusterVersionStatus.Draft;

        return version.IsDefault ? ClusterVersionStatus.Default : ClusterVersionStatus.Previous;
    }
}
