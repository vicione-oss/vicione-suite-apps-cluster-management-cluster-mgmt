using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed class GetClusterInfosConsumer(IClusterDbContext dbContext, IClusterSnapshotStore snapshotStore,
    ILogger<GetClusterInfosConsumer> logger) : RequestConsumer<GetClusterInfosRequest, GetClusterInfosResponse>
{
    private readonly IClusterDbContext _dbContext = dbContext;
    private readonly IClusterSnapshotStore _snapshotStore = snapshotStore;
    private readonly ILogger<GetClusterInfosConsumer> _logger = logger;

    protected override async Task<GetClusterInfosResponse> Respond(ConsumeContext<GetClusterInfosRequest> context)
    {
        _logger.LogDebug("Consume {Request} CorrelationId:{CorrelationId} Id:{Id}",
            nameof(GetClusterInfosResponse),
            context.CorrelationId,
            context.Message.ClusterId);

        var activeInfos = await _dbContext.ActiveClusters.AsNoTracking().ToListAsync(context.CancellationToken);
        var fileInfos = (await _snapshotStore.GetClusterInfos(context.Message.ClusterId, context.CancellationToken))
            .Select(k => ClusterInfoFactory.CreateInfo(k, activeInfos.Any(active => active.Id == k.Id && active.Version == k.Version)))
            .ToList();

        fileInfos.Sort(new ClusterInfoSorter());

        return new GetClusterInfosResponse
        {
            Results = fileInfos
        };
    }

    protected override Task<GetClusterInfosResponse> HandleException(ConsumeContext<GetClusterInfosRequest> context, Exception e)
    {
        _logger.LogError(e, "Failed to handle {NameOfGetClusterInfosResponse}", nameof(GetClusterInfosResponse));

        return Task.FromResult(new GetClusterInfosResponse
        {
            RequestError = ClusterEventFactory.CreateErrorInfo(ClusterError.UnexpectedClusterQueryError, null),
        });
    }
}
