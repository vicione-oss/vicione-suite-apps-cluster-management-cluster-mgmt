using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed partial class RemoveClusterConsumer(IClusterDbContext dbContext, IClusterSnapshotStore store, IClusterLock clusterLock,
    ILogger<RemoveClusterConsumer> logger) :
    IConsumer<Public.Commands.RemoveCluster>, IConsumer<Fault<Public.Commands.RemoveCluster>>
{
    public async Task Consume(ConsumeContext<Public.Commands.RemoveCluster> context)
    {
        LogConsume(logger,
            nameof(Public.Commands.RemoveCluster),
            context.CorrelationId,
            context.Message.ClusterId,
            context.Message.Version);

        if (context.Message.Version == Public.Constants.DefaultClusterVersion)
            throw new InvalidOperationException("Default cluster can't be deleted");

        // A version is never removed while it is being stored
        using (await clusterLock.Acquire(context.Message.ClusterId, context.CancellationToken))
        {
            if (!store.Exists(context.Message.ClusterId, context.Message.Version))
                throw new InvalidOperationException($"Cluster version '{context.Message.Version}' does not exist");

            var clusterIsActive = await dbContext.ActiveClusters.AsNoTracking()
                .AnyAsync(c => c.Id == context.Message.ClusterId
                    && c.Version == context.Message.Version, context.CancellationToken);

            if (clusterIsActive)
                throw new InvalidOperationException($"Active cluster version '{context.Message.Version}' can't be deleted");

            store.Delete(context.Message.ClusterId, context.Message.Version);
        }

        await context.Publish(
            new ClusterChanged(context.Message.CorrelationId, context.Message.ClusterId, CrudAction.Deleted, context.Message.Version)
            {
                CorrelationId = context.Message.CorrelationId
            }, context.CancellationToken);
    }

    /// <summary>
    /// called when an exception happened on consume and all retry attempts failed
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task Consume(ConsumeContext<Fault<Public.Commands.RemoveCluster>> context)
    {
        var command = context.Message.Message;
        var errorEvent = ClusterEventFactory.CreateErrorEvent(command.ClusterId,
            ClusterError.DeleteFailed,
            command.CorrelationId,
            command.Version);

        LogError(logger,
            nameof(Public.Commands.RemoveCluster),
            command.ClusterId,
            command.Version,
            string.Join(" | ", context.Message.Exceptions.Select(e => e.Message)));

        await context.Publish(errorEvent, context.CancellationToken);
    }

    [LoggerMessage(LogLevel.Debug, "Consume {command} CorrelationId={correlationId} Id={clusterId} Version={version}")]
    internal static partial void LogConsume(ILogger<RemoveClusterConsumer> logger, string command, Guid? correlationId, Guid clusterId, Version version);

    [LoggerMessage(LogLevel.Error, "Consume {command} faulted on cluster Id={clusterId} Version={version} with '{message}'")]
    internal static partial void LogError(ILogger<RemoveClusterConsumer> logger, string command, Guid clusterId, Version version, string message);
}
