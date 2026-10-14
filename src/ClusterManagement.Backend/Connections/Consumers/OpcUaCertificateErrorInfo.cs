using ClusterManagement.Internal.Contracts;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Connections.Consumers;

internal static class OpcUaCertificateErrorInfo
{
    public static ErrorInfo Create(Exception e)
        => e is OpcUaCertificateException certificateException
            ? new(certificateException.ErrorCode, certificateException.Message)
            : new(OpcUaCertificateError.UnknownError, e.Message);
}
