using ClusterManagement.Backend.DeployCluster.StateMachines;

namespace ClusterManagement.Backend.DeployCluster.Extensions;

internal static class DeployClusterStateExtensions
{
    public static bool IsCommitVersionEmpty(this DeployClusterState state)
        => state.CommitVersion == DeployClusterState.EmptyVersion;

    /// <summary>
    /// The version being deployed. A deployment that commits a new version has no <see cref="DeployClusterState.TargetVersion"/>
    /// until that version is created, so the <see cref="DeployClusterState.CommitVersion"/> is reported until then.
    /// </summary>
    public static Version ReportedTargetVersion(this DeployClusterState state)
        => state.TargetVersion == DeployClusterState.EmptyVersion ? state.CommitVersion : state.TargetVersion;

    public static bool ShouldRemovePreviousVersion(this DeployClusterState state)
        => state.RemoveVersion != Public.Constants.DefaultClusterVersion
        && state.RemoveVersion != DeployClusterState.EmptyVersion;
}
