using System.Diagnostics.CodeAnalysis;
using Sdk.Messaging;

namespace ClusterManagement.Internal.Requests;

/// <summary>
/// Requests to replace the application certificate of an OPC UA connection by an uploaded certificate with its private key.
/// </summary>
/// <param name="StorePath">The application store path as the connection stores it, relative to the workspace home.</param>
/// <param name="Filename">The file the upload handler stored the PFX or PEM file in; it is deleted afterwards.</param>
/// <param name="Password">The password of the file, <see langword="null"/> if it is not protected.</param>
/// <param name="ApplicationCertificateSubject">The connection's application certificate subject, which the certificate is checked against.</param>
/// <param name="ApplicationUri">The connection's application URI, which the certificate is checked against.</param>
[SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "An OPC UA application URI is not a resolvable URL, it is compared as a string, like the connection's ApplicationUri.")]
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "An OPC UA application URI is not a resolvable URL, it is compared as a string, like the connection's ApplicationUri.")]
public sealed record AddOpcUaApplicationCertificateRequest(
    string StorePath,
    string Filename,
    string? Password,
    string ApplicationCertificateSubject,
    string ApplicationUri) : IRequest<AddOpcUaApplicationCertificateResponse>;
