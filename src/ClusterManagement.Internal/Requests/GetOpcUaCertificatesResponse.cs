using ClusterManagement.Internal.Contracts;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

public sealed record GetOpcUaCertificatesResponse(IReadOnlyCollection<OpcUaCertificateInfo> Certificates) : IResponse
{
    public ErrorInfo? RequestError { get; init; }
}
