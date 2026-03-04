using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed class GetActiveClusterConsumer(IClusterDbContext dbContext, IClusterSnapshotStore store, ILogger<GetActiveClusterConsumer> logger) :
    RequestConsumer<GetActiveClusterRequest, GetActiveClusterResponse>
{
    private readonly IClusterDbContext _dbContext = dbContext;
    private readonly IClusterSnapshotStore _store = store;
    private readonly ILogger<GetActiveClusterConsumer> _logger = logger;

    protected override async Task<GetActiveClusterResponse> Respond(ConsumeContext<GetActiveClusterRequest> context)
    {
        _logger.LogDebug("Consume {Request} CorrelationId:{CorrelationId} Id:{Id}",
            nameof(GetActiveClusterRequest),
            context.CorrelationId,
            context.Message.ClusterId);

        var active = await _dbContext.ActiveClusters.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == context.Message.ClusterId, context.CancellationToken);

        if (active is null)
        {
            return new GetActiveClusterResponse
            {
                RequestError =
                    ClusterEventFactory.CreateErrorInfo(ClusterError.ClusterIdUnknown,
                        context.Message.ClusterId)
            };
        }

        var cluster = await _store.LoadCompressedJson(active.Id, active.Version, context.CancellationToken);
        if (cluster.Length == 0)
        {
            return new GetActiveClusterResponse
            {
                RequestError =
                    ClusterEventFactory.CreateErrorInfo(ClusterError.NoClusterForVersion,
                        context.Message.ClusterId)
            };
        }

        return new GetActiveClusterResponse { CompressedCluster = cluster };
    }

    protected override Task<GetActiveClusterResponse> HandleException(ConsumeContext<GetActiveClusterRequest> context, Exception e)
    {
        _logger.LogError(e, "Failed to handle {ReqeustName}", nameof(GetActiveClusterRequest));

        return Task.FromResult(new GetActiveClusterResponse
        {
            RequestError = ClusterEventFactory.CreateErrorInfo(ClusterError.UnexpectedClusterQueryError, null),
        });
    }
}
