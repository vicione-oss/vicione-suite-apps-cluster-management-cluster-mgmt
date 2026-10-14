using System.IO.Abstractions;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ClusterManagement.Backend.Connections.Extensions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;

namespace ClusterManagement.Backend.Connections;

internal sealed partial class OpcUaCertificateStore(
    IFileSystem fileSystem,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    ILogger<OpcUaCertificateStore> logger) : IOpcUaCertificateStore
{
    // the layout of a directory store of the OPC UA stack, which only loads certificates with the .der extension
    private const string CertificatesDirectory = "certs";
    private const string PrivateKeysDirectory = "private";
    private const string CertificateExtension = ".der";
    private const string PrivateKeyExtension = ".pfx";
    private const string PemCertificateLabel = "CERTIFICATE";
    private const byte DerSequenceTag = 0x30;

    public IReadOnlyCollection<OpcUaCertificateInfo> GetCertificates(string storePath)
        => [.. ReadCertificates(GetCertificatesDirectory(storePath))
            .Select(c => c.Info)
            .OrderBy(c => c.Subject, StringComparer.OrdinalIgnoreCase)];

    public async Task<OpcUaCertificatesResult> AddCertificates(string storePath, string uploadedFile, CancellationToken cancellationToken)
    {
        var certificatesDirectory = GetCertificatesDirectory(storePath);
        if (OpcUaStorePaths.IsApplicationStore(storePath))
            throw new OpcUaCertificateException(OpcUaCertificateError.InvalidStorePath, "The application certificate needs its private key, it is added to its store as such.");

        CheckUploadedFile(uploadedFile);

        List<X509Certificate2> certificates = [];
        try
        {
            // all certificates are loaded before one is stored, so a file with an invalid one adds none
            certificates = LoadCertificates(await fileSystem.File.ReadAllBytesAsync(uploadedFile, cancellationToken));
            var added = OpcUaStorePaths.IsTrustedIssuerStore(storePath) ? certificates : [.. certificates.Where(c => !IssuedAnother(c, certificates))];
            var infos = new List<OpcUaCertificateInfo>(added.Count);
            var storedThumbprints = ReadCertificates(certificatesDirectory)
                .Select(c => c.Info.Thumbprint)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var leftOut = certificates.Count - added.Count;
            if (leftOut > 0)
                LogIssuerCertificatesLeftOut(logger, leftOut, storePath);

            foreach (var certificate in added)
            {
                var info = CreateInfo(certificate);
                infos.Add(info);

                if (storedThumbprints.Contains(info.Thumbprint))
                    continue;

                fileSystem.Directory.CreateDirectory(certificatesDirectory);
                await fileSystem.File.WriteAllBytesAsync(fileSystem.Path.Combine(certificatesDirectory, GetFileName(certificate)), certificate.RawData, cancellationToken);

                LogCertificateAdded(logger, info.Thumbprint, info.Subject, storePath);
            }

            return new(infos, leftOut > 0);
        }
        finally
        {
            certificates.ForEach(c => c.Dispose());
            TryDeleteFile(uploadedFile);
        }
    }

    public async Task<OpcUaApplicationCertificateResult> AddApplicationCertificate(
        string storePath, string uploadedFile, string? password, string applicationCertificateSubject, string applicationUri, CancellationToken cancellationToken)
    {
        var certificatesDirectory = GetCertificatesDirectory(storePath);
        if (!OpcUaStorePaths.IsApplicationStore(storePath))
            throw new OpcUaCertificateException(OpcUaCertificateError.InvalidStorePath, "Only the application store holds a certificate with its private key, which replaces the one it has.");

        CheckUploadedFile(uploadedFile);

        try
        {
            using var certificate = LoadApplicationCertificate(await fileSystem.File.ReadAllBytesAsync(uploadedFile, cancellationToken), password);
            var info = CreateInfo(certificate);
            var privateKeysDirectory = GetPrivateKeysDirectory(certificatesDirectory);
            var certificateFile = fileSystem.Path.Combine(certificatesDirectory, GetFileName(certificate));
            var privateKeyFile = fileSystem.Path.Combine(privateKeysDirectory, fileSystem.Path.ChangeExtension(GetFileName(certificate), PrivateKeyExtension));

            // the new certificate is written before the current one is deleted, so a failure leaves the store as it was;
            // the private key is stored like the OPC UA stack stores the keys it creates: in a PFX without password
            fileSystem.Directory.CreateDirectory(certificatesDirectory);
            fileSystem.Directory.CreateDirectory(privateKeysDirectory);
            await fileSystem.File.WriteAllBytesAsync(privateKeyFile, certificate.Export(X509ContentType.Pkcs12), cancellationToken);
            await fileSystem.File.WriteAllBytesAsync(certificateFile, certificate.RawData, cancellationToken);

            // a store holds one application certificate, the OPC UA stack would not know which one to use otherwise
            DeleteFilesExcept(certificatesDirectory, certificateFile);
            DeleteFilesExcept(privateKeysDirectory, privateKeyFile);

            LogCertificateAdded(logger, info.Thumbprint, info.Subject, storePath);
            return new(info, OpcUaApplicationCertificateCheck.GetWarnings(info, applicationCertificateSubject, applicationUri, Dns.GetHostName()));
        }
        finally
        {
            TryDeleteFile(uploadedFile);
        }
    }

    public void DeleteCertificate(string storePath, string thumbprint)
    {
        var certificatesDirectory = GetCertificatesDirectory(storePath);
        var files = FindCertificateFiles(certificatesDirectory, thumbprint);

        if (files.Count == 0)
            throw new OpcUaCertificateException(OpcUaCertificateError.CertificateNotFound, $"The certificate store has no certificate with the thumbprint '{thumbprint}'.");

        var privateKeysDirectory = GetPrivateKeysDirectory(certificatesDirectory);
        foreach (var file in files)
        {
            fileSystem.File.Delete(file);

            // the OPC UA stack names a private key like its certificate, with an extension depending on its format (.pfx, .pem)
            if (fileSystem.Directory.Exists(privateKeysDirectory))
            {
                var name = fileSystem.Path.GetFileNameWithoutExtension(file);
                foreach (var privateKey in fileSystem.Directory.GetFiles(privateKeysDirectory)
                    .Where(k => fileSystem.Path.GetFileNameWithoutExtension(k) == name))
                {
                    fileSystem.File.Delete(privateKey);
                }
            }
        }

        LogCertificateDeleted(logger, thumbprint, storePath);
    }

    public void DeleteConnectionStores(IEnumerable<string?> storePaths)
    {
        var homeDirectory = fileSystem.Path.GetFullPath(workspaceProvider.Home);

        foreach (var directory in storePaths.Select(OpcUaStorePaths.GetConnectionDirectory).OfType<string>().Distinct())
        {
            var fullDirectory = OpcUaStorePaths.GetFullStorePath(homeDirectory, directory);
            if (!fileSystem.Directory.Exists(fullDirectory))
                continue;

            fileSystem.Directory.Delete(fullDirectory, true);
            LogConnectionStoresDeleted(logger, directory);
        }
    }

    public void CleanupOrphanedStores(IEnumerable<string?> usedStorePaths, TimeSpan minAge)
    {
        var certificatesDirectory = fileSystem.Path.Combine(fileSystem.Path.GetFullPath(workspaceProvider.Home), OpcUaStorePaths.DirectoryName);
        if (!fileSystem.Directory.Exists(certificatesDirectory))
            return;

        var usedDirectories = usedStorePaths.Select(OpcUaStorePaths.GetConnectionDirectory).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var threshold = DateTime.UtcNow - minAge;

        foreach (var directory in fileSystem.Directory.GetDirectories(certificatesDirectory))
        {
            var relativeDirectory = $"{OpcUaStorePaths.DirectoryName}/{fileSystem.Path.GetFileName(directory)}";
            if (usedDirectories.Contains(relativeDirectory) || GetLastWriteTimeUtc(directory) > threshold)
                continue;

            try
            {
                fileSystem.Directory.Delete(directory, true);
                LogOrphanedStoresDeleted(logger, relativeDirectory);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // the next cleanup tries again
                LogOrphanedStoresDeleteFailure(logger, e, relativeDirectory);
            }
        }
    }

    // the stack and the uploads change files deep in a connection's directory, which does not touch the directory itself
    private DateTime GetLastWriteTimeUtc(string directory)
        => fileSystem.Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories)
            .Select(fileSystem.File.GetLastWriteTimeUtc)
            .Append(fileSystem.Directory.GetLastWriteTimeUtc(directory))
            .Max();

    public void CleanupUploadLeftovers(TimeSpan maxAge)
        => fileSystem.DeleteFilesOlderThan(
            fileSystem.GetUploadedOpcUaCertificatesPath(workspaceProvider),
            maxAge,
            path => LogUploadedCertificateDeletion(logger, path),
            (ex, path) => LogUploadedCertificateDeleteFailure(logger, ex, path));

    // the file is deleted afterwards, so only a file the upload handler stored is accepted
    private void CheckUploadedFile(string uploadedFile)
    {
        if (!fileSystem.IsUploadedOpcUaCertificateFile(workspaceProvider, uploadedFile))
            throw new OpcUaCertificateException(OpcUaCertificateError.UploadedFileOutsideUploadDirectory, "The certificate file is not in the upload directory.");

        if (!fileSystem.File.Exists(uploadedFile))
            throw new OpcUaCertificateException(OpcUaCertificateError.UploadedFileNotFound, $"The uploaded certificate file '{fileSystem.Path.GetFileName(uploadedFile)}' was not found.");
    }

    private string GetPrivateKeysDirectory(string certificatesDirectory)
        => fileSystem.Path.Combine(fileSystem.Path.GetDirectoryName(certificatesDirectory)!, PrivateKeysDirectory);

    private void DeleteFilesExcept(string directory, string keptFile)
    {
        foreach (var file in fileSystem.Directory.GetFiles(directory).Where(f => !string.Equals(f, keptFile, StringComparison.Ordinal)))
            fileSystem.File.Delete(file);
    }

    private string GetCertificatesDirectory(string storePath)
    {
        try
        {
            var homeDirectory = fileSystem.Path.GetFullPath(workspaceProvider.Home);
            return fileSystem.Path.Combine(OpcUaStorePaths.GetFullStorePath(homeDirectory, storePath), CertificatesDirectory);
        }
        catch (ArgumentException e)
        {
            throw new OpcUaCertificateException(OpcUaCertificateError.InvalidStorePath, e.Message, e);
        }
    }

    private IReadOnlyList<string> FindCertificateFiles(string certificatesDirectory, string thumbprint)
    {
        if (!fileSystem.Directory.Exists(certificatesDirectory))
            return [];

        // the files of this store and of the OPC UA stack carry the thumbprint in their name, so usually no certificate
        // has to be parsed; only a file someone placed in the directory under another name needs to be
        var marker = $"[{thumbprint}]";
        var named = fileSystem.Directory.GetFiles(certificatesDirectory, "*" + CertificateExtension)
            .Where(f => fileSystem.Path.GetFileName(f).Contains(marker, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return named.Count > 0
            ? named
            : [.. ReadCertificates(certificatesDirectory).Where(c => IsThumbprint(c.Info, thumbprint)).Select(c => c.File)];
    }

    private IEnumerable<(string File, OpcUaCertificateInfo Info)> ReadCertificates(string certificatesDirectory)
    {
        if (!fileSystem.Directory.Exists(certificatesDirectory))
            yield break;

        foreach (var file in fileSystem.Directory.GetFiles(certificatesDirectory, "*" + CertificateExtension))
        {
            OpcUaCertificateInfo info;
            try
            {
                using var certificate = X509CertificateLoader.LoadCertificate(fileSystem.File.ReadAllBytes(file));
                info = CreateInfo(certificate);
            }
            catch (CryptographicException e)
            {
                // the OPC UA stack skips such a file as well
                LogInvalidCertificateFile(logger, e, file);
                continue;
            }

            yield return (file, info);
        }
    }

    private static List<X509Certificate2> LoadCertificates(byte[] content)
    {
        List<X509Certificate2> certificates = [];
        try
        {
            // a DER certificate is an ASN.1 sequence, so it starts with its tag; only other files need decoding as text
            if (content is [DerSequenceTag, ..])
                return [X509CertificateLoader.LoadCertificate(content)];

            // a PEM file is text with one or more labeled sections; all its certificates are read, because issuer
            // certificates often come as one file with the whole chain
            ReadOnlySpan<char> text = Encoding.UTF8.GetString(content);
            while (PemEncoding.TryFind(text, out var fields))
            {
                if (text[fields.Label] is PemCertificateLabel)
                {
                    var certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(text[fields.Base64Data].ToString()));
                    if (certificates.Exists(c => c.Thumbprint == certificate.Thumbprint))
                        certificate.Dispose();
                    else
                        certificates.Add(certificate);
                }

                text = text[fields.Location.End..];
            }

            return certificates.Count > 0 ? certificates : [X509CertificateLoader.LoadCertificate(content)];
        }
        catch (Exception e) when (e is CryptographicException or FormatException)
        {
            certificates.ForEach(c => c.Dispose());
            throw new OpcUaCertificateException(OpcUaCertificateError.InvalidCertificate, "The file is no valid X.509 certificate.", e);
        }
    }

    // an issuer is found by name, like the OPC UA stack builds a chain; a certificate that only shares its name with
    // the issuer of another is left out of a trusted store as well, which trusts less rather than more
    private static bool IssuedAnother(X509Certificate2 certificate, IEnumerable<X509Certificate2> certificates)
        => certificates.Any(c => c != certificate && c.IssuerName.Name == certificate.SubjectName.Name);

    // a PKCS#12 file is a DER sequence as well, a PEM file holds the certificate and its private key, which may be encrypted
    private static X509Certificate2 LoadApplicationCertificate(byte[] content, string? password)
    {
        X509Certificate2 certificate;
        try
        {
            if (content is [DerSequenceTag, ..])
            {
                // exportable, so the key can be stored again without the password
                certificate = X509CertificateLoader.LoadPkcs12(content, password, X509KeyStorageFlags.Exportable);
            }
            else
            {
                var text = Encoding.UTF8.GetString(content);
                if (!text.Contains("PRIVATE KEY-----", StringComparison.Ordinal))
                    throw new OpcUaCertificateException(OpcUaCertificateError.NoPrivateKey, "The file contains no private key.");

                certificate = text.Contains("ENCRYPTED PRIVATE KEY-----", StringComparison.Ordinal)
                    ? X509Certificate2.CreateFromEncryptedPem(text, text, password)
                    : X509Certificate2.CreateFromPem(text, text);
            }
        }
        catch (Exception e) when (e is CryptographicException or ArgumentException)
        {
            // a wrong password and a damaged file cannot be told apart, both fail to decrypt
            throw new OpcUaCertificateException(OpcUaCertificateError.InvalidApplicationCertificate, "The file is no certificate with a private key, or the password is wrong.", e);
        }

        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new OpcUaCertificateException(OpcUaCertificateError.NoPrivateKey, "The file contains no private key.");
        }

        return certificate;
    }

    private static OpcUaCertificateInfo CreateInfo(X509Certificate2 certificate)
        => new(certificate.Thumbprint,
            certificate.GetNameInfo(X509NameType.SimpleName, false) is { Length: > 0 } name ? name : certificate.Subject,
            certificate.Subject,
            certificate.Issuer,
            new(certificate.NotBefore.ToUniversalTime(), TimeSpan.Zero),
            new(certificate.NotAfter.ToUniversalTime(), TimeSpan.Zero),
            OpcUaApplicationCertificateCheck.GetApplicationUri(certificate));

    private static bool IsThumbprint(OpcUaCertificateInfo info, string thumbprint)
        => string.Equals(info.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase);

    // the naming of the OPC UA stack, '<common name> [<thumbprint>].der', with characters invalid on any file system replaced
    private static string GetFileName(X509Certificate2 certificate)
    {
        var name = certificate.GetNameInfo(X509NameType.SimpleName, false);
        var safeName = string.Concat(name.Select(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c) ? '_' : c));

        return $"{safeName} [{certificate.Thumbprint}]{CertificateExtension}".TrimStart();
    }

    private void TryDeleteFile(string file)
    {
        try
        {
            fileSystem.File.Delete(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            LogUploadedCertificateDeleteFailure(logger, e, file);
        }
    }

    [LoggerMessage(LogLevel.Information, "Added OPC UA certificate '{Thumbprint}' ({Subject}) to store '{StorePath}'")]
    private static partial void LogCertificateAdded(ILogger logger, string thumbprint, string subject, string storePath);

    [LoggerMessage(LogLevel.Information, "Left {Count} CA certificates of an uploaded chain out of the trusted store '{StorePath}'")]
    private static partial void LogIssuerCertificatesLeftOut(ILogger logger, int count, string storePath);

    [LoggerMessage(LogLevel.Information, "Deleted OPC UA certificate '{Thumbprint}' from store '{StorePath}'")]
    private static partial void LogCertificateDeleted(ILogger logger, string thumbprint, string storePath);

    [LoggerMessage(LogLevel.Warning, "Skipping OPC UA certificate file '{Path}', it is no valid certificate")]
    private static partial void LogInvalidCertificateFile(ILogger logger, Exception exception, string path);

    [LoggerMessage(LogLevel.Information, "Deleted the OPC UA certificate stores '{Directory}' of a deleted connection")]
    private static partial void LogConnectionStoresDeleted(ILogger logger, string directory);

    [LoggerMessage(LogLevel.Information, "Deleted the OPC UA certificate stores '{Directory}', which no connection uses")]
    private static partial void LogOrphanedStoresDeleted(ILogger logger, string directory);

    [LoggerMessage(LogLevel.Warning, "Failed to delete the OPC UA certificate stores '{Directory}', which no connection uses")]
    private static partial void LogOrphanedStoresDeleteFailure(ILogger logger, Exception exception, string directory);

    [LoggerMessage(LogLevel.Information, "Deleting stale uploaded OPC UA certificate '{Path}'")]
    private static partial void LogUploadedCertificateDeletion(ILogger logger, string path);

    [LoggerMessage(LogLevel.Warning, "Failed to delete uploaded OPC UA certificate '{Path}'")]
    private static partial void LogUploadedCertificateDeleteFailure(ILogger logger, Exception exception, string path);
}
