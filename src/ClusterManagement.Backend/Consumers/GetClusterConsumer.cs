using ClusterManagement.Backend.Extensions;
using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Events;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed class GetClusterConsumer(IClusterSnapshotStore snapshotStore, ILogger<GetClusterConsumer> logger) :
    RequestConsumer<GetClusterRequest, GetClusterResponse>
{
    private readonly IClusterSnapshotStore _snapshotStore = snapshotStore;
    private readonly ILogger<GetClusterConsumer> _logger = logger;

    public override async Task<GetClusterResponse> Respond(GetClusterRequest message, CancellationToken cancellationToken)
    {
        var requestVersion = message.ClusterVersion
            ?? await _snapshotStore.GetLatestVersion(message.ClusterId, cancellationToken);

        var cluster = await _snapshotStore.LoadCompressedJson(message.ClusterId,
            requestVersion,
            cancellationToken);

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
                    message.ClusterId,
                    message.ClusterVersion)
        };
    }

    public override Task<GetClusterResponse> HandleException(GetClusterRequest message, Exception e, CancellationToken cancellationToken)
    {
        _logger.LogError(e, "Failed to handle {Request}", nameof(GetClusterRequest));

        return Task.FromResult(new GetClusterResponse
        {
            RequestError = ClusterEventFactory.CreateErrorInfo(ClusterError.UnexpectedClusterQueryError, null),
        });
    }
}
