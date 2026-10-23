using ClusterManagement.Backend.DeployCluster.StateMachines;

namespace ClusterManagement.Backend.DeployCluster.Extensions;

internal static class DeployClusterStateExtensions
{
    public static bool IsCommitVersionEmpty(this DeployClusterState state)
        => state.CommitVersion == DeployClusterState.EmptyVersion;

    /// <summary>
    /// The commit reply confirmed that the <see cref="DeployClusterState.CommitVersion"/> was created, so it's the target version.
    /// </summary>
    public static bool IsCommitVersionCreated(this DeployClusterState state)
        => !state.IsCommitVersionEmpty() && state.TargetVersion == state.CommitVersion;

    /// <summary>
    /// The version being deployed. A deployment that commits a new version has no <see cref="DeployClusterState.TargetVersion"/>
    /// until that version is created, so the <see cref="DeployClusterState.CommitVersion"/> is reported until then.
    /// </summary>
    public static Version ReportedTargetVersion(this DeployClusterState state)
        => state.TargetVersion == DeployClusterState.EmptyVersion ? state.CommitVersion : state.TargetVersion;

    /// <summary>
    /// The target version is the active version: it was activated, or the active version is deployed again.
    /// Whether every application runs it is checked separately where a redeploy matters.
    /// </summary>
    public static bool IsTargetVersionActive(this DeployClusterState state)
        => state.ActiveVersion == state.TargetVersion;

    public static bool ShouldRemovePreviousVersion(this DeployClusterState state)
        => state.RemoveVersion != Public.Constants.DefaultClusterVersion
        && state.RemoveVersion != DeployClusterState.EmptyVersion;
}
