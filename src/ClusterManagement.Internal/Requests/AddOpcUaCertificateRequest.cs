using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

/// <summary>
/// Requests to add an uploaded certificate to the certificate store of an OPC UA connection.
/// </summary>
/// <param name="StorePath">The store path as the connection stores it, relative to the workspace home.</param>
/// <param name="Filename">The file the upload handler stored the certificate in; it is deleted afterwards.</param>
public sealed record AddOpcUaCertificateRequest(string StorePath, string Filename) : IRequest<AddOpcUaCertificateResponse>;
