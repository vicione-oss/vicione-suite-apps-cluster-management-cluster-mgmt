using System.IO.Abstractions;
using System.IO.Compression;
using ClusterManagement.Backend.Designs.Consumers;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.Logging;
using Sdk.Backend.Modules;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Designs;

internal sealed partial class PackageStore(
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IFileSystem fileSystem,
    IClusterDependencyStore dependencyStore,
    ILogger<PackageStore> logger) : IPackageStore
{
    public async Task<ClusterDependency> StorePackage(byte[] zipArchive, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream(zipArchive);
        using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

        var (packageName, packageVersion) = await VerifyPackageArchive(archive, cancellationToken);

        ExtractPackageArchive(archive, packageName, packageVersion);

        var clusterDependency = new ClusterDependency
        {
            Name = packageName,
            Version = new Version(packageVersion)
        };

        var operation = new ClusterDependencyOperation(clusterDependency, ClusterDependencyOperationKind.Install);

        // Queue the package for update on next restart
        await dependencyStore.EnqueueOperations([operation], cancellationToken);

        // Now a restart is required to actually load the package
        return clusterDependency;
    }

    private async Task<(string Name, string Version)> VerifyPackageArchive(ZipArchive packageArchive, CancellationToken cancellationToken)
    {
        var depsJsonEntry = packageArchive.Entries.FirstOrDefault(e => e.Name.EndsWith("deps.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The provided package archive does not contain a deps.json file.");

        // We could add validation of core version etc.
        var packageName = depsJsonEntry.Name.Replace(".deps.json", "", StringComparison.OrdinalIgnoreCase);
        var dllEntryName = $"{packageName}.dll";
        var packageVersion = await GetPackageVersion(depsJsonEntry, packageName, cancellationToken);

        await VerifyPackageDll(packageArchive, packageName, cancellationToken);

        return (packageName, packageVersion);
    }

    private async Task VerifyPackageDll(ZipArchive packageArchive, string packageName, CancellationToken cancellationToken)
    {
        try
        {
            var dllEntry = packageArchive.Entries.FirstOrDefault(e => e.Name.Equals($"{packageName}.dll", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"The provided package archive does not contain the required '{packageName}.dll' file.");

            using var depsJsonStream = dllEntry.Open();
            using var memoryStream = new MemoryStream();

            await depsJsonStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            var (targetFramework, runtimeVersion, platformTarget) = AssemblyMetadataProvider.GetAssemblyInfo(memoryStream);

            logger.LogInformation("Package {Name} supports Framework: {TargetFramework}, Runtime Version: {RuntimeVersion}, Platform Target: {PlatformTarget}",
            dllEntry.Name, targetFramework, runtimeVersion, platformTarget);

            // We could check here if the package is compatible with our system in more detail
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to verify upload package assembly for '{PackageName}'", packageName);
        }
    }

    private void ExtractPackageArchive(ZipArchive packageArchive, string packageName, string packageVersion)
    {
        var dependenciesPath = fileSystem.GetDependenciesPath(workspaceProvider);
        var packageDestinationPath = fileSystem.Path.Combine(dependenciesPath, packageName, packageVersion);

        // Package already exists we assume it's not a productive system
        if (fileSystem.Directory.Exists(packageDestinationPath))
        {
            // What if the package is still in use by engine host?
            // EH probably is able to continue with previous version loaded into memory
            // If the FB is not loaded yet, but requested afterwards by LoadContext it will be a problem
            logger.LogWarning("Removing existing package '{PackageName}' path '{Name}'", packageName, packageDestinationPath);
            fileSystem.Directory.Delete(packageDestinationPath, true);
        }

        // Extract package to our packages folder. It won't be loaded from there until we add it to our dependency store.
        logger.LogInformation("Uploading package '{PackageName}' version '{PackageVersion}' to path '{Name}'", packageName, packageVersion, packageDestinationPath);
        fileSystem.Directory.CreateDirectory(packageDestinationPath);
        packageArchive.ExtractToDirectory(packageDestinationPath);
    }

    private static async Task<string> GetPackageVersion(ZipArchiveEntry depsJsonEntry, string packageName, CancellationToken cancellationToken)
    {
        using var depsJsonStream = depsJsonEntry.Open();
        using var reader = new StreamReader(depsJsonStream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (!line.Contains(packageName, StringComparison.Ordinal))
                continue;

            if (PackageDepsJsonRegex.Parse(line, out var version))
                return version;
        }

        throw new InvalidOperationException($"Package version of '{packageName}' not found in deps.json.");
    }

    public async Task DeletePackage(ClusterDependency dependency, CancellationToken cancellationToken)
    {
        // Check if we already have loaded the package
        var dependencies = await dependencyStore.LoadDependencies(cancellationToken);
        var inUse = dependencies.Any(k => k.Name == dependency.Name && k.Version == dependency.Version);

        // Is it even possible to delete a package that is not in use?
        if (!inUse)
        {
            var dependenciesPath = fileSystem.GetDependenciesPath(workspaceProvider);
            var packageDestinationPath = fileSystem.Path.Combine(dependenciesPath, dependency.Name, dependency.Version.ToString());

            if (fileSystem.Directory.Exists(packageDestinationPath))
            {
                logger?.LogInformation("Deleting package '{PackageName}' with path '{Name}'", dependency.Name, packageDestinationPath);
                fileSystem.Directory.Delete(packageDestinationPath, true);
            }
        }

        // Queue the package for uninstall on next restart - it might have been uploaded but not used yet
        var operation = new ClusterDependencyOperation(dependency, ClusterDependencyOperationKind.Uninstall);

        await dependencyStore.EnqueueOperations([operation], CancellationToken.None);
    }
}
