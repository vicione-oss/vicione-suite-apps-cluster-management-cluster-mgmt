using System.IO.Abstractions;
using ClusterManagement.Internal.Contracts;
using Sdk.Backend.Modules;
using Semver;

namespace ClusterManagement.Backend.Designs.Extensions;

internal static class IFileSystemExtensions
{
    internal const string DependenciesDirectory = "Dependencies";
    internal const string ReplacedDependenciesDirectory = "ReplacedDependencies";
    internal const string PackageStagingDirectory = "PackageStaging";

    public static string GetDependenciesPath(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws)
        => fileSystem.Path.Combine(ws.Cache, DependenciesDirectory);

    /// <summary>
    /// Gets the directory an existing package version is moved to while it gets replaced.
    /// It must be outside of the dependencies directory, which is scanned for installed packages.
    /// </summary>
    public static string GetReplacedDependenciesPath(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws)
        => fileSystem.Path.Combine(ws.Cache, ReplacedDependenciesDirectory);

    /// <summary>
    /// Gets the directory a package is extracted to before it is moved into the dependencies directory.
    /// It must be outside of the dependencies directory but on the same volume, so the move is a rename.
    /// </summary>
    public static string GetPackageStagingPath(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws)
        => fileSystem.Path.Combine(ws.Cache, PackageStagingDirectory);

    /// <summary>
    /// Gets the directory the client's stream upload handler stores uploaded package archives in.
    /// </summary>
    public static string GetUploadedPackagesPath(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws)
        => fileSystem.Path.Combine(ws.Cache, ClusterPackageUpload.DirectoryName);

    /// <summary>
    /// Returns whether <paramref name="filePath"/> names a file directly inside the upload directory of package archives.
    /// </summary>
    public static bool IsUploadedPackageFile(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws, string filePath)
    {
        var fullPath = fileSystem.Path.GetFullPath(filePath);
        var uploadDirectory = fileSystem.Path.GetFullPath(fileSystem.GetUploadedPackagesPath(ws));

        // The relative path is the bare file name only for a file directly inside the directory; ".." segments,
        // subdirectories and paths on another drive all yield something else.
        return fileSystem.Path.GetRelativePath(uploadDirectory, fullPath) == fileSystem.Path.GetFileName(fullPath);
    }

    internal static IDictionary<PackageInfo, string> GetComponents(this IFileSystem fileSystem, string directory)
    {
        Dictionary<PackageInfo, string> components = [];

        var mainComponentFilenames = fileSystem.Directory.GetFiles(directory, "*.deps.json", SearchOption.AllDirectories);

        foreach (var mainComponentFilename in mainComponentFilenames)
        {
            var versionDirectory = fileSystem.Path.GetDirectoryName(mainComponentFilename) ?? string.Empty;
            var rawVersion = fileSystem.Path.GetFileName(versionDirectory);
            var packageName = fileSystem.Path.GetFileName(fileSystem.Path.GetDirectoryName(versionDirectory)) ?? string.Empty;

            if (string.IsNullOrEmpty(rawVersion) || string.IsNullOrEmpty(packageName))
                throw new InvalidOperationException("The packages directory should have the following structure: 'packages/MyPackage/1.2.3'.");

            if (!SemVersion.TryParse(rawVersion, out var version))
                throw new InvalidOperationException($"The version of the package '{packageName}' is not a valid SemVer. Or the directory structure is not correct. (Expected: 'packages/MyPackage/1.2.3')");

            if (!fileSystem.Path.IsPathRooted(versionDirectory))
                versionDirectory = fileSystem.Path.GetFullPath(versionDirectory);

            if (!components.TryAdd(new(packageName, version), new(versionDirectory)))
                throw new InvalidOperationException($"The package '{packageName}' with version '{version}' is already added.");
        }

        return components;
    }
}
