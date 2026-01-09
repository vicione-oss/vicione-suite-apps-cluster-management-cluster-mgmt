using System.Globalization;
using System.Text;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Extensions;

namespace ClusterManagement.Backend.DeployCluster.StateMachines;

public sealed partial class DeployClusterStateMachine
{
    private bool AreAllDeploymentsUpdated(DeployClusterState saga)
    {
        var allAppsUpdated = CollectionsEqual(saga.ClusterApplicationsToUpdate, saga.ClusterApplicationsUpdateFinished.Keys);
        if (allAppsUpdated)
        {
            _logger.LogInformation("{Name} appsFinished:{AppsFinished} at least one failed:{OneFailed}",
                nameof(AreAllDeploymentsUpdated),
                string.Join(", ", saga.ClusterApplicationsUpdateFinished.Keys),
                saga.AtLeastOneDeployReturnedError);

            LogErrors();
            return true;
        }

        _logger.LogInformation("{Name} appsToUpdate:{AppsToUpdate} appsFinished:{AppsFinished} at least one failed:{OneFailed}",
            nameof(AreAllDeploymentsUpdated),
            string.Join(", ", saga.ClusterApplicationsToUpdate.Except(saga.ClusterApplicationsUpdateFinished.Keys)),
            string.Join(", ", saga.ClusterApplicationsUpdateFinished.Keys),
            saga.AtLeastOneDeployReturnedError);

        LogErrors();

        return allAppsUpdated;

        void LogErrors()
        {
            var errs = saga.ClusterApplicationsUpdateFinished.Where(k => k.Value is not null).ToArray();
            if (errs.Length <= 0)
                return;

            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0} errors:", nameof(AreAllDeploymentsUpdated));

            for (var i = 0; i < errs.Length; i++)
            {
                if (i == errs.Length - 1)
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0} - {1}", errs[i].Key, errs[i].Value?.Message);
                else
                    sb.AppendFormattedLine("{0} - {1}", errs[i].Key, errs[i].Value?.Message);
            }

            _logger.LogWarning("{Message}", sb.ToString());
        }
    }

    private bool AreAllNodesAddedAndAppsRunning(DeployClusterState saga)
        => AreAllNodesAdded(saga) && AreAllAppsStarted(saga);

    private bool AreAllNodesAdded(DeployClusterState saga)
    {
        var allNodesCreated = CollectionsEqual(saga.ClusterNodesToCreate.Keys, saga.ClusterNodesCreated);

        _logger.LogInformation("{Name} nodesToCreate:{NodesToCreate} nodesCreated:{NodesCreated} equal:{Equal}",
            nameof(AreAllNodesAdded),
            string.Join(", ", saga.ClusterNodesToCreate),
            string.Join(", ", saga.ClusterNodesCreated),
            allNodesCreated);

        return allNodesCreated;
    }

    private bool AreAllAppsStarted(DeployClusterState saga)
    {
        var allNodesStarted = CollectionsEqual(saga.ClusterNodesToCreate.Values.Where(a => a is not null).Cast<Guid>(),
            saga.ClusterApplicationStarted);

        _logger.LogInformation("{Name} appsToStart:{AppsToStart} appsStarted:{AppsCreated} equal:{Equal}",
            nameof(AreAllNodesAdded),
            string.Join(", ", saga.ClusterNodesToCreate.Values.Where(a => a is not null).Cast<Guid>()),
            string.Join(", ", saga.ClusterApplicationStarted),
            allNodesStarted);

        return allNodesStarted;
    }

    private bool AreAllClusterNodesCleaned(DeployClusterState saga)
    {
        var nodesCleaned = CollectionsEqual(saga.ClusterNodesToClean, saga.ClusterNodesCleaned);
        _logger.LogInformation("{Name} nodesToClean:{NodesToClean} nodesCleaned:{NodesCleaned} equal:{Equal}",
            nameof(AreAllClusterNodesCleaned),
            string.Join(", ", saga.ClusterNodesToClean),
            string.Join(", ", saga.ClusterNodesCleaned),
            nodesCleaned);
        return nodesCleaned;
    }
    private static bool CollectionsEqual(IEnumerable<Guid>? requested, IEnumerable<Guid> returned)
        => !requested?.Except(returned).Any() ?? true;

    private void LogEvent<TMessage>(BehaviorContext<DeployClusterState, TMessage> context, string message) where TMessage : class
        => _logger.LogInformation(
            "Saga[{CorrelationId}][{Event}] {Message}",
            context.CorrelationId,
            context.Message.GetType().Name,
            message);

    private void LogDeployDuration(DeployClusterState saga)
        => _logger.LogInformation(
            "Saga[{CorrelationId}]] Deployment duration: {Duration}",
            saga.CorrelationId,
            (DateTime.UtcNow - saga.Started).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture));
}
