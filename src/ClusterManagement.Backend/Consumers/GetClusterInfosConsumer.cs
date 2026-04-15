using ClusterManagement.Backend.DbContext;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed class GetClusterInfosConsumer(IClusterDbContext dbContext, IClusterSnapshotStore snapshotStore,
    ILogger<GetClusterInfosConsumer> logger) : RequestConsumer<GetClusterInfosRequest, GetClusterInfosResponse>
{
    public override async Task<GetClusterInfosResponse> Respond(GetClusterInfosRequest message, CancellationToken cancellationToken)
    {
        var activeInfos = await dbContext.ActiveClusters.AsNoTracking().ToListAsync(cancellationToken);
        var fileInfos = (await snapshotStore.GetClusterInfos(message.ClusterId, cancellationToken))
            .Select(k => ClusterInfoFactory.CreateInfo(k, activeInfos.Any(active => active.Id == k.Id && active.Version == k.Version)))
            .ToList();

        fileInfos.Sort(new ClusterInfoSorter());

        return new GetClusterInfosResponse
        {
            Results = fileInfos
        };
    }

    public override Task<GetClusterInfosResponse> HandleException(GetClusterInfosRequest message, Exception e, CancellationToken cancellationToken)
    {
        logger.LogHandleException(e, nameof(GetClusterInfosResponse));

        return Task.FromResult(new GetClusterInfosResponse
        {
            RequestError = ClusterEventFactory.CreateErrorInfo(ClusterError.UnexpectedClusterQueryError, null),
        });
    }
}
