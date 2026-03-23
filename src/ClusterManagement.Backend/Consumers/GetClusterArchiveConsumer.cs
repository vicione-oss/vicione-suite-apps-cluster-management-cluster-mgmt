using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed class GetClusterArchiveConsumer(IClusterSnapshotStore snapshotStore, ILogger<GetClusterArchiveConsumer> logger) :
    RequestConsumer<GetClusterArchiveRequest, GetClusterArchiveResponse>
{
    private readonly IClusterSnapshotStore _snapshotStore = snapshotStore;
    private readonly ILogger<GetClusterArchiveConsumer> _logger = logger;

    public override async Task<GetClusterArchiveResponse> Respond(GetClusterArchiveRequest message, CancellationToken cancellationToken)
    {
        var clusterZip = await _snapshotStore.LoadCompressedJson(message.ClusterId, message.ClusterVersion, cancellationToken);

        if (clusterZip.Length == 0)
        {
            _logger.LogDebug("No matching cluster found");
            return new GetClusterArchiveResponse([]);
        }
        return new GetClusterArchiveResponse(clusterZip);
    }

    public override Task<GetClusterArchiveResponse> HandleException(GetClusterArchiveRequest message, Exception e, CancellationToken cancellationToken)
    {
        _logger.LogError(e, "Failed to create archive for cluster {Id} v.{Version}", message.ClusterId, message.ClusterVersion);
        return Task.FromResult(new GetClusterArchiveResponse([]) { RequestError = new ErrorInfo(-100, e.Message) });
    }
}
