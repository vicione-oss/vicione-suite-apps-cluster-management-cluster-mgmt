using ClusterManagement.Backend.Snapshots;
using ClusterManagement.Public.Requests;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Messaging;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Consumers;

public sealed partial class GetClusterArchiveConsumer(IClusterSnapshotStore snapshotStore, ILogger<GetClusterArchiveConsumer> logger) :
    RequestConsumer<GetClusterArchiveRequest, GetClusterArchiveResponse>
{
    public override async Task<GetClusterArchiveResponse> Respond(GetClusterArchiveRequest message, CancellationToken cancellationToken)
    {
        var clusterZip = await snapshotStore.LoadCompressedJson(message.ClusterId, message.ClusterVersion, cancellationToken);

        if (clusterZip.Length == 0)
        {
            LogNoClusterFound(logger);
            return new GetClusterArchiveResponse([]);
        }
        return new GetClusterArchiveResponse(clusterZip);
    }

    public override Task<GetClusterArchiveResponse> HandleException(GetClusterArchiveRequest message, Exception e, CancellationToken cancellationToken)
    {
        LogHandleException(logger, e, message.ClusterId, message.ClusterVersion);
        return Task.FromResult(new GetClusterArchiveResponse([]) { RequestError = new ErrorInfo(-100, e.Message) });
    }

    [LoggerMessage(LogLevel.Debug, "No matching cluster found")]
    private static partial void LogNoClusterFound(ILogger<GetClusterArchiveConsumer> logger);

    [LoggerMessage(LogLevel.Error, "Failed to create archive for cluster {Id} version {Version}")]
    private static partial void LogHandleException(ILogger<GetClusterArchiveConsumer> logger, Exception exception, Guid id, Version version);
}
