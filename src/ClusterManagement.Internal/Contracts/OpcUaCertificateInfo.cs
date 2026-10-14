using System.Diagnostics.CodeAnalysis;
namespace ClusterManagement.Internal.Contracts;

/// <summary>
/// A certificate in the certificate store of an OPC UA connection.
/// </summary>
/// <param name="Thumbprint">The SHA-1 thumbprint as a hex string, which identifies the certificate in its store.</param>
/// <param name="Name">The common name of the subject, or the subject if it has none, for display.</param>
/// <param name="Subject">The distinguished name of the subject.</param>
/// <param name="Issuer">The distinguished name of the issuer.</param>
/// <param name="NotBefore">The start of the certificate's validity.</param>
/// <param name="NotAfter">The end of the certificate's validity.</param>
/// <param name="ApplicationUri">The application URI in the subject alternative name, which an OPC UA application certificate carries.</param>
[SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "An OPC UA application URI is not a resolvable URL, it is compared as a string, like the connection's ApplicationUri.")]
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "An OPC UA application URI is not a resolvable URL, it is compared as a string, like the connection's ApplicationUri.")]
public sealed record OpcUaCertificateInfo(string Thumbprint, string Name, string Subject, string Issuer, DateTimeOffset NotBefore, DateTimeOffset NotAfter, string? ApplicationUri = null);
