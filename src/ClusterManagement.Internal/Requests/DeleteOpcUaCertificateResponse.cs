using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

public sealed record DeleteOpcUaCertificateResponse : IResponse
{
    public ErrorInfo? RequestError { get; init; }
}
