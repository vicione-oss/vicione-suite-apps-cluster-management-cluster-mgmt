using ClusterManagement.Internal.Contracts;

namespace ClusterManagement.Backend.Connections;

/// <param name="Certificates">The certificates of the file the store holds now.</param>
/// <param name="IssuerCertificatesLeftOut">Whether CA certificates of the file were not added, because they belong in the trusted issuer store.</param>
public sealed record OpcUaCertificatesResult(IReadOnlyList<OpcUaCertificateInfo> Certificates, bool IssuerCertificatesLeftOut);
