using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed partial class GetActiveClusterConsumer(IClusterDbContext dbContext, IClusterSnapshotStore store, ILogger<GetActiveClusterConsumer> logger) :
    RequestConsumer<GetActiveClusterRequest, GetActiveClusterResponse>
{
    public override async Task<GetActiveClusterResponse> Respond(GetActiveClusterRequest message, CancellationToken cancellationToken)
    {
        var active = await dbContext.ActiveClusters.AsNoTracking()
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

        var cluster = await store.LoadCompressedJson(active.Id, active.Version, cancellationToken);
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
        logger.LogHandleException(e, nameof(GetActiveClusterRequest));

        return Task.FromResult(new GetActiveClusterResponse
        {
            RequestError = ClusterEventFactory.CreateErrorInfo(ClusterError.UnexpectedClusterQueryError, null),
        });
    }
}
