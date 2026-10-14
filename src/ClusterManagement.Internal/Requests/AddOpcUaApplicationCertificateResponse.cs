using ClusterManagement.Internal.Contracts;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

/// <param name="Certificate">The added certificate, <see langword="null"/> if adding it failed.</param>
/// <param name="Warnings">The settings of the connection the added certificate does not match.</param>
public sealed record AddOpcUaApplicationCertificateResponse(OpcUaCertificateInfo? Certificate, IReadOnlyCollection<OpcUaApplicationCertificateWarning> Warnings) : IResponse
{
    public ErrorInfo? RequestError { get; init; }
}
