using ClusterManagement.Backend.DeployCluster.Contracts;
using ClusterManagement.Internal;
using MassTransit;
using MassTransit.Saga;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClusterManagement.Backend.DeployCluster.StateMachines;

/// <summary>
/// Sends <see cref="DeployClusterStateTimeout"/> to every deploy cluster saga whose <see cref="DeployClusterState.StateTimeoutAt"/> has passed,
/// on every <see cref="DeploySagaTimeoutOptions.CheckInterval"/>.
/// </summary>
/// <remarks>
/// The saga can't schedule its timeouts itself: delayed messages need the delayed message exchange plugin on RabbitMQ,
/// which the brokers don't have (suite ADR-004, D3). It's registered with the saga, so it only runs on a master or standalone instance.
/// </remarks>
internal sealed partial class DeploySagaTimeoutWatchdog(
    IQuerySagaRepository<DeployClusterState> sagas,
    IBus bus,
    IOptions<ClusterManagementOptions> options,
    ILogger<DeploySagaTimeoutWatchdog> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.DeploySagaTimeouts.CheckInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await SendTimeouts(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // the module stopped
        }
    }

    private async Task SendTimeouts(CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTime.UtcNow;
            var overdue = await sagas.Find(new SagaQuery<DeployClusterState>(saga => saga.StateTimeoutAt <= now));

            // A saga that doesn't process the timeout, e.g. while its endpoint is down, gets it again on the next check
            foreach (var correlationId in overdue)
            {
                LogStateOverdue(logger, correlationId);
                await bus.Publish(new DeployClusterStateTimeout(correlationId), cancellationToken);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // the next check tries again
            LogCheckFailed(logger, e);
        }
    }

    [LoggerMessage(LogLevel.Debug, "Saga[{CorrelationId}] is overdue in its state, sending its state timeout")]
    private static partial void LogStateOverdue(ILogger logger, Guid correlationId);

    [LoggerMessage(LogLevel.Warning, "Checking the deployments for state timeouts failed")]
    private static partial void LogCheckFailed(ILogger logger, Exception exception);
}
