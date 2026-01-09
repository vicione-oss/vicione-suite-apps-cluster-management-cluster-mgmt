namespace ClusterManagement.Backend.DeployCluster.StateMachines;

public sealed partial class DeployClusterStateMachine
{
    private bool AreAllDeploymentsUpdated(DeployClusterState saga)
    {
        var allAppsUpdated = CollectionsEqual(saga.ClusterApplicationsToUpdate, saga.ClusterApplicationsUpdateFinished.Keys);
        if (allAppsUpdated)
        {
            LogAllDeploymentsUpdated(_logger,
                saga.CorrelationId,
                string.Join(", ", saga.ClusterApplicationsUpdateFinished.Keys),
                saga.AtLeastOneDeployReturnedError);

            LogErrors();
            return true;
        }

        LogDeploymentsUpdated(_logger,
            saga.CorrelationId,
            string.Join(", ", saga.ClusterApplicationsToUpdate.Except(saga.ClusterApplicationsUpdateFinished.Keys)),
            saga.AtLeastOneDeployReturnedError);

        LogErrors();

        return allAppsUpdated;

        void LogErrors()
        {
            var errs = saga.ClusterApplicationsUpdateFinished.Where(k => k.Value is not null).ToArray();
            if (errs.Length <= 0)
                return;

            foreach (var error in errs)
            {
                LogDeploymentsError(_logger, saga.CorrelationId, error.Key, error.Value?.Message);
            }
        }
    }

    private bool AreAllNodesAddedAndAppsRunning(DeployClusterState saga)
        => AreAllNodesAdded(saga) && AreAllAppsStarted(saga);

    private bool AreAllNodesAdded(DeployClusterState saga)
    {
        var allNodesCreated = CollectionsEqual(saga.ClusterNodesToCreate.Keys, saga.ClusterNodesCreated);
        if (allNodesCreated)
        {
            LogAllNodesAdded(_logger, saga.CorrelationId, string.Join(", ", saga.ClusterNodesCreated));
        }
        else
        {
            LogNodesAdded(_logger, saga.CorrelationId, string.Join(", ", saga.ClusterNodesToCreate));
        }

        return allNodesCreated;
    }

    private bool AreAllAppsStarted(DeployClusterState saga)
    {
        var allNodesStarted = CollectionsEqual(saga.ClusterNodesToCreate.Values.Where(a => a is not null).Cast<Guid>(),
            saga.ClusterApplicationStarted);

        if (allNodesStarted)
        {
            LogAllAppsStarted(
                _logger,
                saga.CorrelationId,
                string.Join(", ", saga.ClusterNodesToCreate.Values.Where(a => a is not null).Cast<Guid>()));
        }
        else
        {
            LogAppsStarted(_logger, saga.CorrelationId, string.Join(", ", saga.ClusterApplicationStarted));
        }

        return allNodesStarted;
    }

    private bool AreAllClusterNodesCleaned(DeployClusterState saga)
    {
        var allNodesCleaned = CollectionsEqual(saga.ClusterNodesToClean, saga.ClusterNodesCleaned);
        if (allNodesCleaned)
        {
            LogAllNodesCleaned(_logger, saga.CorrelationId, string.Join(", ", saga.ClusterNodesCleaned));
        }
        else
        {
            LogNodesCleaned(_logger, saga.CorrelationId, string.Join(", ", saga.ClusterNodesToClean));
        }

        return allNodesCleaned;
    }
    private static bool CollectionsEqual(IEnumerable<Guid>? requested, IEnumerable<Guid> returned)
        => !requested?.Except(returned).Any() ?? true;
}
