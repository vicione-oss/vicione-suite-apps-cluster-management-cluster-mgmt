using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed class GetClusterConsumer(IClusterSnapshotStore snapshotStore, ILogger<GetClusterConsumer> logger) :
    RequestConsumer<GetClusterRequest, GetClusterResponse>
{
    private readonly IClusterSnapshotStore _snapshotStore = snapshotStore;
    private readonly ILogger<GetClusterConsumer> _logger = logger;

    protected override async Task<GetClusterResponse> Respond(ConsumeContext<GetClusterRequest> context)
    {
        _logger.LogDebug("Consume {Request} CorrelationId:{CorrelationId} Id:{Id} Version:{Version}",
            nameof(GetClusterRequest),
            context.CorrelationId,
            context.Message.ClusterId,
            context.Message.ClusterVersion);

        var requestVersion = context.Message.ClusterVersion
            ?? await _snapshotStore.GetLatestVersion(context.Message.ClusterId, context.CancellationToken);

        var cluster = await _snapshotStore.LoadCompressedJson(context.Message.ClusterId,
            requestVersion,
            context.CancellationToken);

        if (cluster.Length > 0)
        {
            return new GetClusterResponse
            {
                CompressedCluster = cluster
            };
        }

        return new GetClusterResponse
        {
            RequestError =
                ClusterEventFactory.CreateErrorInfo(ClusterError.NoClusterForVersion,
                    context.Message.ClusterId,
                    context.Message.ClusterVersion)
        };
    }

    protected override Task<GetClusterResponse> HandleException(ConsumeContext<GetClusterRequest> context, Exception e)
    {
        _logger.LogError(e, "Failed to handle {Request}", nameof(GetClusterRequest));

        return Task.FromResult(new GetClusterResponse
        {
            RequestError = ClusterEventFactory.CreateErrorInfo(ClusterError.UnexpectedClusterQueryError, null),
        });
    }
}
