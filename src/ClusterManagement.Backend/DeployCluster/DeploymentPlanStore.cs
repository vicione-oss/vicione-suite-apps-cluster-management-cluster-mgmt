using ClusterManagement.Public.Events;

namespace ClusterManagement.Backend.DeployCluster;

/// <summary>
/// Keeps the latest plan of the update and of the rollback each instance published for the recent deployments of each
/// cluster, so a client that starts watching a deployment while it runs knows how far the instances are.
/// </summary>
/// <remarks>
/// An instance can still publish plans of an earlier deployment after the next one started, when the earlier one timed
/// out during its update, so the deployments are kept apart and only the oldest is dropped once a cluster has more than
/// <see cref="DeploymentsPerCluster"/>. For the same reason an instance can still publish plans of its update after
/// its rollback began, so the plans of the two runs are kept apart as well.
/// </remarks>
public sealed class DeploymentPlanStore
{
    internal const int DeploymentsPerCluster = 2;

    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, List<(Guid CorrelationId, Dictionary<(Guid ClusterApplicationId, bool IsRollback), ClusterNodeDeploymentPlanned> Plans)>> _byCluster = [];

    public void Keep(ClusterNodeDeploymentPlanned plan)
    {
        lock (_lock)
        {
            if (!_byCluster.TryGetValue(plan.ClusterId, out var deployments))
            {
                deployments = [];
                _byCluster[plan.ClusterId] = deployments;
            }

            var deployment = deployments.Find(d => d.CorrelationId == plan.CorrelationId);
            if (deployment.Plans is null)
            {
                deployment = (plan.CorrelationId, []);
                deployments.Add(deployment);
                if (deployments.Count > DeploymentsPerCluster)
                    deployments.RemoveAt(0);
            }

            deployment.Plans[(plan.ClusterApplicationId, plan.IsRollback)] = plan;
        }
    }

    /// <summary>
    /// The latest plan of the update and of the rollback of each instance for the deployment of <paramref name="clusterId"/> with
    /// <paramref name="correlationId"/>; empty when no plan of that deployment arrived.
    /// </summary>
    public IReadOnlyList<ClusterNodeDeploymentPlanned> Of(Guid clusterId, Guid correlationId)
    {
        lock (_lock)
        {
            return _byCluster.TryGetValue(clusterId, out var deployments)
                && deployments.Find(d => d.CorrelationId == correlationId) is { Plans: { } plans }
                ? [.. plans.Values]
                : [];
        }
    }
}
