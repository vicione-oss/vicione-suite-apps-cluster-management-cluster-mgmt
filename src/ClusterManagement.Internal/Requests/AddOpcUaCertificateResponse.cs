using ClusterManagement.Internal.Contracts;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

/// <param name="Certificates">The certificates of the uploaded file the store holds now; empty if adding them failed.</param>
/// <param name="IssuerCertificatesLeftOut">Whether CA certificates of an uploaded chain were not added to a trusted store, because they belong in the trusted issuer store.</param>
public sealed record AddOpcUaCertificateResponse(IReadOnlyCollection<OpcUaCertificateInfo> Certificates, bool IssuerCertificatesLeftOut) : IResponse
{
    public ErrorInfo? RequestError { get; init; }
}
