using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

/// <summary>
/// Requests the certificates in the certificate store of an OPC UA connection.
/// </summary>
/// <param name="StorePath">The store path as the connection stores it, relative to the workspace home.</param>
public sealed record GetOpcUaCertificatesRequest(string StorePath) : IRequest<GetOpcUaCertificatesResponse>;
