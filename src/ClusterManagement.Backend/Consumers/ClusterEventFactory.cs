using ClusterManagement.Public.Events;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Consumers;

internal static class ClusterEventFactory
{
    public static ClusterError CreateErrorEvent(Guid clusterId, Version clusterVersion, int code, Guid correlationId)
        => new()
        {
            ClusterId = clusterId,
            Error = CreateErrorInfo(code, clusterId, clusterVersion),
            CorrelationId = correlationId
        };

    public static ClusterError CreateErrorEvent(Guid clusterId, int code, Guid correlationId)
        => new()
        {
            ClusterId = clusterId,
            Error = CreateErrorInfo(code, clusterId),
            CorrelationId = correlationId
        };

    public static ErrorInfo CreateErrorInfo(int code, Guid? clusterId, Version? clusterVersion = null)
        => code switch
        {
            ClusterError.PublishFailed => new ErrorInfo(code,
                $"Failed to publish version {clusterVersion} of cluster {clusterId}"),
            ClusterError.DeleteFailed => new ErrorInfo(code,
                $"Unable to delete cluster {clusterId} from database"),
            ClusterError.ClusterIdUnknown => new ErrorInfo(code,
                $"Unable to find cluster {clusterId} in database"),
            ClusterError.NoClusterForVersion => new ErrorInfo(code,
                $"No cluster (id: {clusterId}) with version {clusterVersion} found!"),
            _ => new ErrorInfo(code, $"Unknown operation error ClusterManagement {clusterId}")
        };
}
