using System.IO.Abstractions;
using Sdk.Backend.Modules;
using Semver;

namespace ClusterManagement.Backend.Designs.Extensions;

internal static class IFileSystemExtensions
{
    private const string DependenciesDirectory = "Dependencies";

    public static string GetDependenciesPath(this IFileSystem fileSystem, IWorkspaceProvider<ClusterManagementBackendModule> ws)
        => fileSystem.Path.Combine(ws.Cache, DependenciesDirectory);

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
