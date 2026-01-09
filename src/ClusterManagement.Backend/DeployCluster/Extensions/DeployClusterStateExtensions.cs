using ClusterManagement.Backend.DeployCluster.StateMachines;

namespace ClusterManagement.Backend.DeployCluster.Extensions;

internal static class DeployClusterStateExtensions
{
    public static bool IsCommitVersionEmpty(this DeployClusterState state)
        => state.CommitVersion == DeployClusterState.EmptyVersion;

    public static bool ShouldRemovePreviousVersion(this DeployClusterState state)
        => state.RemoveVersion != Public.Constants.DefaultClusterVersion
        && state.RemoveVersion != DeployClusterState.EmptyVersion;
}
