using System.IO.Abstractions;
using System.IO.Compression;
using ClusterManagement.Backend.Designs.Consumers;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
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
    public async Task<ClusterDependency> StorePackage(string filePath, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        using var stream = fileSystem.File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = OpenPackageArchive(stream);

        var (packageName, packageVersion) = await VerifyPackageArchive(archive, cancellationToken);

        await VerifyPackageVersionNotInstalled(packageName, packageVersion, cancellationToken);

        ExtractPackageArchive(archive, packageName, packageVersion);

        var clusterDependency = new ClusterDependency
        {
            Name = packageName,
            Version = packageVersion
        };

        var operation = new ClusterDependencyOperation(clusterDependency, ClusterDependencyOperationKind.Install);

        // Queue the package for update on next restart
        await dependencyStore.EnqueueOperations([operation], cancellationToken);

        // Now a restart is required to actually load the package
        return clusterDependency;
    }

    private static ZipArchive OpenPackageArchive(Stream stream)
    {
        ZipArchive archive;

        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read);
        }
        catch (InvalidDataException ex)
        {
            throw new PackageValidationException(ClusterPackageChanged.NotAZipArchive, "The provided package file is not a zip archive.", ex);
        }

        if (archive.Entries.Count == 0)
        {
            archive.Dispose();
            throw new PackageValidationException(ClusterPackageChanged.EmptyArchive, "The provided package archive does not contain any files.");
        }

        return archive;
    }

    private async Task<(string Name, string Version)> VerifyPackageArchive(ZipArchive packageArchive, CancellationToken cancellationToken)
    {
        var depsJsonEntry = packageArchive.Entries.FirstOrDefault(e => e.Name.EndsWith("deps.json", StringComparison.OrdinalIgnoreCase))
            ?? throw new PackageValidationException(ClusterPackageChanged.InvalidPackageContent, "The provided package archive does not contain a deps.json file.");

        // We could add validation of core version etc.
        var packageName = depsJsonEntry.Name.Replace(".deps.json", "", StringComparison.OrdinalIgnoreCase);
        var dllEntryName = $"{packageName}.dll";
        var packageVersion = await GetPackageVersion(depsJsonEntry, packageName, cancellationToken);

        await VerifyPackageDll(packageArchive, packageName, cancellationToken);

        return (packageName, packageVersion);
    }

    private async Task VerifyPackageDll(ZipArchive packageArchive, string packageName, CancellationToken cancellationToken)
    {
        var dllEntry = packageArchive.Entries.FirstOrDefault(e => e.Name.Equals($"{packageName}.dll", StringComparison.OrdinalIgnoreCase))
            ?? throw new PackageValidationException(ClusterPackageChanged.InvalidPackageContent, $"The provided package archive does not contain the required '{packageName}.dll' file.");

        try
        {
            using var depsJsonStream = await dllEntry.OpenAsync(cancellationToken);
            using var memoryStream = new MemoryStream();

            await depsJsonStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            var (targetFramework, runtimeVersion, platformTarget) = AssemblyMetadataProvider.GetAssemblyInfo(memoryStream);

            logger.LogPackageVerification(dllEntry.Name, targetFramework, runtimeVersion, platformTarget);

            // We could check here if the package is compatible with our system in more detail
        }
        catch (Exception)
        {
            logger.LogPackageVerificationFailure(packageName);
        }
    }

    /// <summary>
    /// An installed version is loaded by the engine hosts from the dependencies directory, so it must not be replaced.
    /// A version that was only uploaded (and not yet installed by a restart) can be replaced.
    /// </summary>
    private async Task VerifyPackageVersionNotInstalled(string packageName, string packageVersion, CancellationToken cancellationToken)
    {
        var dependencies = await dependencyStore.LoadDependencies(cancellationToken);

        if (dependencies.Any(k => k.Name == packageName && k.Version.ToString() == packageVersion))
            throw new PackageValidationException(ClusterPackageChanged.PackageVersionInUse,
                $"The package '{packageName}' version '{packageVersion}' is installed and cannot be replaced.");
    }

    /// <summary>
    /// Extracts the package into a staging directory first and swaps it into the dependencies directory only once
    /// extraction has succeeded, so a failed extraction never touches an existing version.
    /// </summary>
    private void ExtractPackageArchive(ZipArchive packageArchive, string packageName, string packageVersion)
    {
        var dependenciesPath = fileSystem.GetDependenciesPath(workspaceProvider);
        var packageDestinationPath = fileSystem.Path.Combine(dependenciesPath, packageName, packageVersion);

        // The staging directory is on the same volume as the dependencies directory, so the swap below is a rename.
        var stagingPath = fileSystem.Path.Combine(fileSystem.GetPackageStagingPath(workspaceProvider), Guid.NewGuid().ToString("N"));

        logger.LogPackageUpload(packageName, packageVersion, packageDestinationPath);

        try
        {
            fileSystem.Directory.CreateDirectory(stagingPath);
            packageArchive.ExtractToDirectory(stagingPath);
        }
        catch (Exception ex)
        {
            TryDeleteDirectory(stagingPath);

            // A damaged entry (e.g. truncated upload) cannot be fixed by a retry.
            if (ex is InvalidDataException)
                throw new PackageValidationException(ClusterPackageChanged.CorruptArchive, $"The provided package archive of '{packageName}' is damaged and could not be extracted.", ex);

            throw;
        }

        // It won't be loaded from the dependencies directory until we add it to our dependency store.
        string? replacedPackagePath;

        try
        {
            replacedPackagePath = MoveAsideExistingPackage(packageName, packageDestinationPath);
        }
        catch
        {
            TryDeleteDirectory(stagingPath);
            throw;
        }

        try
        {
            fileSystem.Directory.CreateDirectory(fileSystem.Path.Combine(dependenciesPath, packageName));
            fileSystem.Directory.Move(stagingPath, packageDestinationPath);
        }
        catch
        {
            if (replacedPackagePath is not null)
                TryRestoreReplacedPackage(packageName, replacedPackagePath, packageDestinationPath);

            TryDeleteDirectory(stagingPath);
            throw;
        }

        if (replacedPackagePath is not null)
            TryDeleteDirectory(replacedPackagePath);
    }

    /// <summary>
    /// Moves an existing package version out of the dependencies directory, so it can be restored if the new one can't be moved in.
    /// </summary>
    /// <returns>The path the existing version was moved to, or <see langword="null"/> if there was none.</returns>
    private string? MoveAsideExistingPackage(string packageName, string packageDestinationPath)
    {
        if (!fileSystem.Directory.Exists(packageDestinationPath))
            return null;

        var replacedDependenciesPath = fileSystem.GetReplacedDependenciesPath(workspaceProvider);
        var replacedPackagePath = fileSystem.Path.Combine(replacedDependenciesPath, Guid.NewGuid().ToString("N"));

        logger.LogPackageReplacing(packageName, packageDestinationPath, replacedPackagePath);

        fileSystem.Directory.CreateDirectory(replacedDependenciesPath);
        fileSystem.Directory.Move(packageDestinationPath, replacedPackagePath);

        return replacedPackagePath;
    }

    private void TryRestoreReplacedPackage(string packageName, string replacedPackagePath, string packageDestinationPath)
    {
        try
        {
            logger.LogReplacedPackageRestore(packageName, packageDestinationPath);
            fileSystem.Directory.Move(replacedPackagePath, packageDestinationPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Keep the original exception, it tells why the new version could not be put in place.
            logger.LogReplacedPackageRestoreFailure(ex, packageName, replacedPackagePath);
        }
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            if (fileSystem.Directory.Exists(path))
                fileSystem.Directory.Delete(path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover outside of the dependencies directory does no harm, it is removed on the next start.
            logger.LogPackageDirectoryDeleteFailure(ex, path);
        }
    }

    public void CleanupLeftovers(TimeSpan uploadedPackageMaxAge)
    {
        TryDeleteDirectory(fileSystem.GetReplacedDependenciesPath(workspaceProvider));
        TryDeleteDirectory(fileSystem.GetPackageStagingPath(workspaceProvider));

        // Archives whose AddClusterPackage was never consumed (e.g. circuit gone, backend down).
        // Recent ones are kept, the client may still be writing or about to send them.
        fileSystem.DeleteFilesOlderThan(
            fileSystem.GetUploadedPackagesPath(workspaceProvider),
            uploadedPackageMaxAge,
            logger.LogUploadedPackageDeletion,
            logger.LogUploadedPackageDeleteFailure);
    }

    private static async Task<string> GetPackageVersion(ZipArchiveEntry depsJsonEntry, string packageName, CancellationToken cancellationToken)
    {
        using var depsJsonStream = await depsJsonEntry.OpenAsync(cancellationToken);
        using var reader = new StreamReader(depsJsonStream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (!line.Contains(packageName, StringComparison.Ordinal))
                continue;

            if (PackageDepsJsonRegex.Parse(line, out var version))
                return version;
        }

        throw new PackageValidationException(ClusterPackageChanged.InvalidPackageContent, $"Package version of '{packageName}' not found in deps.json.");
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
            var packageVersionPath = fileSystem.Path.Combine(dependenciesPath, dependency.Name, dependency.Version.ToString());

            if (!fileSystem.Directory.Exists(packageVersionPath))
            {
                logger.LogDeletePackageFailureWarning(dependency.Name, packageVersionPath);
                return;
            }

            logger.LogPackageVersionDeletion(dependency.Name, dependency.Version, packageVersionPath);
            fileSystem.Directory.Delete(packageVersionPath, true);

            var packagePath = fileSystem.Path.Combine(dependenciesPath, dependency.Name);
            if (fileSystem.Directory.GetFileSystemEntries(packagePath).Length == 0)
            {
                logger.LogEmptyPackageFolderDeletion(dependency.Name, packagePath);
                fileSystem.Directory.Delete(packagePath, true);
            }
            return;
        }

        // Queue the package for uninstall on next restart - it might have been uploaded but not used yet
        var operation = new ClusterDependencyOperation(dependency, ClusterDependencyOperationKind.Uninstall);

        await dependencyStore.EnqueueOperations([operation], CancellationToken.None);
    }
}
