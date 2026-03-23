using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
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

    public override async Task<GetActiveClusterResponse> Respond(GetActiveClusterRequest message, CancellationToken cancellationToken)
    {
        var active = await _dbContext.ActiveClusters.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == message.ClusterId, cancellationToken);

        if (active is null)
        {
            return new GetActiveClusterResponse
            {
                RequestError =
                    ClusterEventFactory.CreateErrorInfo(ClusterError.ClusterIdUnknown,
                        message.ClusterId)
            };
        }

        var cluster = await _store.LoadCompressedJson(active.Id, active.Version, cancellationToken);
        if (cluster.Length == 0)
        {
            return new GetActiveClusterResponse
            {
                RequestError =
                    ClusterEventFactory.CreateErrorInfo(ClusterError.NoClusterForVersion,
                        message.ClusterId)
            };
        }

        return new GetActiveClusterResponse { CompressedCluster = cluster };
    }

    public override Task<GetActiveClusterResponse> HandleException(GetActiveClusterRequest message, Exception e, CancellationToken cancellationToken)
    {
        _logger.LogError(e, "Failed to handle {ReqeustName}", nameof(GetActiveClusterRequest));

        return Task.FromResult(new GetActiveClusterResponse
        {
            RequestError = ClusterEventFactory.CreateErrorInfo(ClusterError.UnexpectedClusterQueryError, null),
        });
    }
}
