using System.Text.RegularExpressions;
using ClusterManagement.Public.Connections;
using ClusterManagement.Public.Connections.Extensions;
using Sdk.Connections.Contracts;

namespace ClusterManagement.Backend.Connections;

/// <summary>
/// The paths of the certificate stores of OPC UA connections, which cluster management places below its workspace home.
/// </summary>
/// <remarks>
/// The stores are below the workspace home rather than the cache, so the certificates survive clearing the cache.
/// <para />
/// A connection stores its paths relative to the workspace home, with forward slashes, so they stay valid when a backup is
/// restored or a cluster is exported to an instance with a different home directory or operating system. Data collection
/// wizard puts them unchanged into the OPC UA DataPort's properties (see <c>GetCertificateStoreProperties</c>), and they are
/// made absolute at deploy time.
/// <para />
/// Connections created with 3.0.0-beta2 store the paths absolute, or relative to the executable if the workspace home is
/// configured relative, with the separators of the operating system they were created on. They are recognized by their
/// <c>ua-certs/&lt;id&gt;/&lt;store&gt;</c> ending wherever a store path is read, see <see cref="NormalizeStorePath"/>.
/// </remarks>
internal static partial class OpcUaStorePaths
{
    internal const string DirectoryName = "ua-certs";
    private const string ApplicationStoreName = "application";
    private const string TrustedIssuerStoreName = "trusted-issuer";

    /// <summary>
    /// Creates the relative paths of the certificate stores of a new connection, below a directory of its own.
    /// </summary>
    public static (string Application, string Trusted, string TrustedIssuer) CreateStorePaths()
    {
        // every connection gets a directory of its own, so connections never share or overwrite each other's certificates
        var directory = $"{DirectoryName}/{Guid.NewGuid():N}";

        return ($"{directory}/{ApplicationStoreName}", $"{directory}/trusted", $"{directory}/{TrustedIssuerStoreName}");
    }

    /// <summary>
    /// Gets the store paths of an OPC UA client or server connection as the connection stores them; none for any other connection.
    /// </summary>
    public static IEnumerable<string?> GetStorePaths(Connection connection)
    {
        if (connection.GetOpcUaClientConnection() is { } client)
            return [client.ApplicationCertificatesPath, client.TrustedCertificatesPath, client.TrustedIssuerCertificatesPath];

        if (connection.GetOpcUaServerConnection() is { } server)
            return [server.ApplicationCertificatesPath, server.TrustedCertificatesPath, server.TrustedIssuerCertificatesPath];

        return [];
    }

    /// <summary>
    /// Returns whether <paramref name="path"/> is a relative store path below the certificates directory, as created by
    /// <see cref="CreateStorePaths"/>.
    /// </summary>
    /// <remarks>
    /// Other relative paths are not ours: the OPC UA stack also accepts e.g. <c>CurrentUser\My</c> for a Windows store or
    /// <c>%CommonApplicationData%/...</c>, which must reach the engine unchanged.
    /// </remarks>
    public static bool IsStorePath(string path)
        => path.StartsWith(DirectoryName + "/", StringComparison.Ordinal) && path.Length > DirectoryName.Length + 1;

    /// <summary>
    /// Returns the relative store path for a store path as 3.0.0-beta2 created it, e.g. <c>ua-certs/0123/trusted</c> for
    /// <c>/mnt/persistent/AppData/ClusterManagement/ua-certs/0123/trusted</c> or <c>C:\AppData\ua-certs\0123\trusted</c>;
    /// any other path is returned unchanged.
    /// </summary>
    /// <remarks>
    /// The ending identifies the store wherever the workspace home was, so the stores are found on another instance as well,
    /// e.g. after a backup was restored there.
    /// </remarks>
    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(path))]
    public static string? NormalizeStorePath(string? path)
        => path is null || IsStorePath(path) || LegacyStorePath().Match(path) is not { Success: true } match
            ? path
            : $"{DirectoryName}/{match.Groups["id"].Value}/{match.Groups["store"].Value}";

    /// <summary>
    /// Returns whether <paramref name="path"/> is the store of a connection's application certificate, rather than one of
    /// the stores of the certificates it trusts.
    /// </summary>
    public static bool IsApplicationStore(string path)
        => NormalizeStorePath(path).EndsWith("/" + ApplicationStoreName, StringComparison.Ordinal);

    /// <summary>
    /// Returns whether <paramref name="path"/> is the store of the CA certificates that a connection verifies certificate
    /// chains with, rather than a store of the certificates it trusts.
    /// </summary>
    public static bool IsTrustedIssuerStore(string path)
        => NormalizeStorePath(path).EndsWith("/" + TrustedIssuerStoreName, StringComparison.Ordinal);

    /// <summary>
    /// Returns the directory of a connection that <paramref name="path"/> is a store of, e.g. <c>ua-certs/0123</c> for
    /// <c>ua-certs/0123/trusted</c>; <see langword="null"/> if it is no store path.
    /// </summary>
    public static string? GetConnectionDirectory(string? path)
    {
        path = NormalizeStorePath(path);
        if (path is null || !IsStorePath(path))
            return null;

        var name = path[(DirectoryName.Length + 1)..].Split('/')[0];
        return name is "" or "." or ".." ? null : $"{DirectoryName}/{name}";
    }

    /// <summary>
    /// Returns the absolute path of the store path <paramref name="path"/> below <paramref name="homeDirectory"/>.
    /// </summary>
    /// <remarks>
    /// A store path of 3.0.0-beta2 is made relative first, see <see cref="NormalizeStorePath"/>.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="path"/> is no store path or leaves the certificates directory.</exception>
    public static string GetFullStorePath(string homeDirectory, string path)
    {
        path = NormalizeStorePath(path);
        if (!IsStorePath(path))
            throw new ArgumentException($"'{path}' is no OPC UA certificate store path.", nameof(path));

        var certificatesDirectory = Path.GetFullPath(Path.Combine(homeDirectory, DirectoryName));
        var fullPath = Path.GetFullPath(Path.Combine(homeDirectory, path));

        // GetFullPath resolves '..' segments, which must not lead out of the certificates directory
        if (!fullPath.StartsWith(certificatesDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException($"'{path}' is outside of the OPC UA certificates directory.", nameof(path));

        return fullPath;
    }

    /// <summary>
    /// Returns the value an OPC UA DataPort's store path property is passed to the engine with: a store path is made absolute
    /// below <paramref name="homeDirectory"/>, any other property or value is returned unchanged.
    /// </summary>
    public static object? ResolveDataPortProperty(string dataPortDesignId, string propertyId, object? value, string homeDirectory)
        => dataPortDesignId is OpcUaDataPort.ClientDesignId or OpcUaDataPort.ServerDesignId
            && propertyId is OpcUaDataPort.ApplicationCertificatesStorePathPropertyId
                or OpcUaDataPort.TrustedCertificatesStorePathPropertyId
                or OpcUaDataPort.TrustedIssuerCertificatesStorePathPropertyId
            && value is string path
            && TryGetFullStorePath(homeDirectory, path, out var fullPath)
                ? fullPath
                : value;

    // a path that leads out of the certificates directory is not a store of a connection either, it reaches the engine
    // unchanged like any other path, so one wrong property does not fail the whole deployment
    private static bool TryGetFullStorePath(string homeDirectory, string path, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? fullPath)
    {
        fullPath = null;
        if (!IsStorePath(NormalizeStorePath(path)))
            return false;

        try
        {
            fullPath = GetFullStorePath(homeDirectory, path);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // the ending of a store path as 3.0.0-beta2 created it: <home>/ua-certs/<id without hyphens>/<store>
    [GeneratedRegex(@"(?:^|[\\/])ua-certs[\\/](?<id>[0-9a-fA-F]{32})[\\/](?<store>application|trusted-issuer|trusted)[\\/]?$")]
    private static partial Regex LegacyStorePath();
}
