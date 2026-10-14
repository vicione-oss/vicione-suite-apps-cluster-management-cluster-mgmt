using System.Diagnostics.CodeAnalysis;
using ClusterManagement.Internal.Contracts;

namespace ClusterManagement.Backend.Connections;

/// <summary>
/// Manages the certificates in the certificate stores of OPC UA connections.
/// </summary>
/// <remarks>
/// A store path is the relative path a connection stores, see <see cref="OpcUaStorePaths"/>. The stores are directory
/// stores of the OPC UA stack: certificates are DER files in <c>certs</c>, private keys are files of the same name in <c>private</c>.
/// </remarks>
[SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "An OPC UA application URI is not a resolvable URL, it is compared as a string, like the connection's ApplicationUri.")]
public interface IOpcUaCertificateStore
{
    /// <summary>
    /// Gets the certificates in the store; a store that does not exist yet has none.
    /// </summary>
    /// <exception cref="OpcUaCertificateException">The store path is invalid.</exception>
    IReadOnlyCollection<OpcUaCertificateInfo> GetCertificates(string storePath);

    /// <summary>
    /// Adds the certificates the upload handler stored in <paramref name="uploadedFile"/> to the store and deletes the file.
    /// </summary>
    /// <remarks>
    /// The file may be a DER certificate or a PEM file with one or more certificates, like a CA chain; they are stored DER
    /// encoded, because the OPC UA stack only loads DER files. If one of them is invalid, none is added.
    /// <para />
    /// The trusted issuer store gets all certificates of a chain. A trusted store gets only those that issued none of the
    /// others, because the OPC UA stack trusts every certificate a trusted CA issued; a CA certificate uploaded on its own
    /// is added, to trust the whole CA.
    /// <para />
    /// A certificate that is already in the store is not added a second time, it is returned all the same.
    /// </remarks>
    /// <exception cref="OpcUaCertificateException">The store path, the uploaded file or a certificate is invalid.</exception>
    Task<OpcUaCertificatesResult> AddCertificates(string storePath, string uploadedFile, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the certificate in the application store by the certificate and private key the upload handler stored in
    /// <paramref name="uploadedFile"/>, and deletes the file.
    /// </summary>
    /// <remarks>
    /// The file may be a PKCS#12 (PFX) file or a PEM file with the certificate and its private key, which may be encrypted.
    /// The certificate is checked against the connection's <paramref name="applicationCertificateSubject"/> and
    /// <paramref name="applicationUri"/>; a mismatch does not prevent adding it, it is reported as a warning.
    /// </remarks>
    /// <exception cref="OpcUaCertificateException">The store path, the uploaded file, its password or the certificate is invalid.</exception>
    Task<OpcUaApplicationCertificateResult> AddApplicationCertificate(
        string storePath, string uploadedFile, string? password, string applicationCertificateSubject, string applicationUri, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the certificate with <paramref name="thumbprint"/> and its private key, if there is one, from the store.
    /// </summary>
    /// <exception cref="OpcUaCertificateException">The store path is invalid or the store has no such certificate.</exception>
    void DeleteCertificate(string storePath, string thumbprint);

    /// <summary>
    /// Deletes the directory of a connection that <paramref name="storePaths"/> are stores of, with all its certificates.
    /// </summary>
    /// <remarks>
    /// Store paths of 3.0.0-beta2 are recognized like everywhere else, any other path is ignored.
    /// </remarks>
    void DeleteConnectionStores(IEnumerable<string?> storePaths);

    /// <summary>
    /// Deletes the directories below the certificates directory that no store of <paramref name="usedStorePaths"/> is in,
    /// unless something in them changed within <paramref name="minAge"/>.
    /// </summary>
    /// <remarks>
    /// A new connection gets its directory with its first upload, before it is saved, so a recent directory may still
    /// belong to a connection that is being created.
    /// </remarks>
    void CleanupOrphanedStores(IEnumerable<string?> usedStorePaths, TimeSpan minAge);

    /// <summary>
    /// Deletes uploaded certificates that were never added to a store and are older than <paramref name="maxAge"/>.
    /// </summary>
    void CleanupUploadLeftovers(TimeSpan maxAge);
}
