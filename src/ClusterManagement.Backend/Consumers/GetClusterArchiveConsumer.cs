using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Requests;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed class GetClusterArchiveConsumer(IClusterSnapshotStore snapshotStore, ILogger<GetClusterArchiveConsumer> logger) :
    RequestConsumer<GetClusterArchiveRequest, GetClusterArchiveResponse>
{
    private readonly IClusterSnapshotStore _snapshotStore = snapshotStore;
    private readonly ILogger<GetClusterArchiveConsumer> _logger = logger;

    protected override async Task<GetClusterArchiveResponse> Respond(ConsumeContext<GetClusterArchiveRequest> context)
    {
        var clusterZip = await _snapshotStore.LoadCompressedJson(context.Message.ClusterId, context.Message.ClusterVersion, context.CancellationToken);

        if (clusterZip.Length == 0)
        {
            _logger.LogDebug("No matching cluster found");
            return new GetClusterArchiveResponse([]);
        }
        return new GetClusterArchiveResponse(clusterZip);
    }

    protected override Task<GetClusterArchiveResponse> HandleException(ConsumeContext<GetClusterArchiveRequest> context, Exception e)
    {
        _logger.LogError(e, "Failed to create archive for cluster {Id} v.{Version}", context.Message.ClusterId, context.Message.ClusterVersion);
        return Task.FromResult(new GetClusterArchiveResponse([]) { RequestError = new ErrorInfo(-100, e.Message) });
    }
}
