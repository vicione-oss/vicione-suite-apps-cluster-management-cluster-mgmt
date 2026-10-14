using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

/// <summary>
/// Requests to delete a certificate, and its private key if the store has one, from the certificate store of an OPC UA connection.
/// </summary>
/// <param name="StorePath">The store path as the connection stores it, relative to the workspace home.</param>
/// <param name="Thumbprint">The thumbprint of the certificate.</param>
public sealed record DeleteOpcUaCertificateRequest(string StorePath, string Thumbprint) : IRequest<DeleteOpcUaCertificateResponse>;
