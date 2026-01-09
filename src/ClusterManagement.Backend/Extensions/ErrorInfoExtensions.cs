using ClusterManagement.Backend.DeployCluster;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Extensions;

public static class ErrorInfoExtensions
{
    public static ClusterUpdateException AsException(this ErrorInfo errorInfo, Exception? innerException = null)
        => new(errorInfo.Message, innerException, errorInfo.ErrorCode);
}
