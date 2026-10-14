using ClusterManagement.Internal.Contracts;

namespace ClusterManagement.Backend.Connections;

/// <param name="Certificate">The added application certificate.</param>
/// <param name="Warnings">The settings of the connection the certificate does not match.</param>
public sealed record OpcUaApplicationCertificateResult(OpcUaCertificateInfo Certificate, IReadOnlyCollection<OpcUaApplicationCertificateWarning> Warnings);
