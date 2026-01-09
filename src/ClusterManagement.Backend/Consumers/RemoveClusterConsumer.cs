using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed class RemoveClusterConsumer(IClusterDbContext dbContext, IClusterSnapshotStore store, ILogger<RemoveClusterConsumer> logger) :
    IConsumer<Public.Commands.RemoveCluster>, IConsumer<Fault<Public.Commands.RemoveCluster>>
{
    private readonly IClusterDbContext _dbContext = dbContext;
    private readonly IClusterSnapshotStore _store = store;
    private readonly ILogger<RemoveClusterConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<Public.Commands.RemoveCluster> context)
    {
        _logger.LogInformation("Consume {Command} CorrelationId:{CorrelationId} Id:{Id}",
            nameof(Public.Commands.RemoveCluster), context.CorrelationId, context.Message.ClusterId);

        if (!_store.Exists(context.Message.ClusterId, context.Message.Version))
            return;

        var clusterIsActive = await _dbContext.ActiveClusters.AsNoTracking()
            .AnyAsync(c => c.Id == context.Message.ClusterId
                && c.Version == context.Message.Version, context.CancellationToken);

        if (clusterIsActive)
            throw new InvalidOperationException($"Active cluster v{context.Message.Version} can't be deleted");
        if (context.Message.Version == Public.Constants.DefaultClusterVersion)
            throw new InvalidOperationException("Default cluster can't be deleted");

        _store.Delete(context.Message.ClusterId, context.Message.Version);

        await context.Publish(
            new ClusterChanged(context.Message.ClusterId, CrudAction.Deleted, context.Message.Version)
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
        _logger.LogError("{Command} faulted with errors: {Errors}",
            nameof(Public.Commands.RemoveCluster),
            string.Join(" | ", context.Message.Exceptions.Select(e => e.Message)));

        await context.Publish(ClusterEventFactory.CreateErrorEvent(context.Message.Message.ClusterId,
            ClusterError.DeleteFailed, context.Message.Message.CorrelationId), context.CancellationToken);
    }
}
