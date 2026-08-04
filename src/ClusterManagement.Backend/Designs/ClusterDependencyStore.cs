using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Reflection;
using System.Text.Json;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Internal;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Backend.IO;
using Sdk.Backend.Modules;
using Sdk.Messaging;
using Semver;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Designs;

internal sealed class ClusterDependencyStore(
    IFileSystem fileSystem,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IOptions<ClusterManagementOptions> options,
    IAtomicFileWriter atomicFileWriter,
    ILogger<ClusterDependencyStore> logger) : IClusterDependencyStore
{
    internal const string PackagesFileName = "packages";
    internal const string PackagesFileExtension = "json";
    private const string PackagesUpdateFileName = "packages-ops.json";
    internal const string PackagesRessourceFolder = "Packages";

    /// <summary>
    /// Returns the path to the packages file. If a variant is specified, the path will be suffixed with the variant.
    /// Variants are always separated by a dot and be lowercase. If the variant is null or empty, the path will be
    /// without variant. 
    /// e.g. packages.json, packages.variant.json
    /// </summary>
    internal static string GetPackagesFilepath(string baseDirectory, string? variant, IFileSystem fileSystem)
        => fileSystem.Path.Combine(baseDirectory, GetPackagesFilename(variant));

    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
    internal static string GetPackagesFilename(string? variant)
        => string.IsNullOrEmpty(variant)
            ? $"{PackagesFileName}.{PackagesFileExtension}"
            : $"{PackagesFileName}.{variant.ToLowerInvariant()}.{PackagesFileExtension}";

    private string GetUpdateFilePath()
        => fileSystem.Path.Combine(workspaceProvider.Home, PackagesUpdateFileName);

    /// <summary>
    /// If the packages file set configured by <see cref="ClusterManagementOptions.PackageSet"/> does not exist,
    /// the initial packages file is copied from the embedded resources to the workspace.
    /// </summary>
    public async Task Initialize(CancellationToken stoppingToken)
    {
        var name = string.Join('.', typeof(ClusterManagementBackendModule).Namespace, PackagesRessourceFolder, GetPackagesFilename(options.Value.PackageSet));
        var destination = GetPackagesFilepath(workspaceProvider.Home, options.Value.PackageSet, fileSystem);

        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Failed to access internal package set resource");

        // A corrupted packages file (e.g. caused by a power loss during a write) must not brick startup,
        // in that case the file is restored from the embedded resources.
        var existingPackages = fileSystem.File.Exists(destination)
            ? await TryLoadDependencies(destination, stoppingToken)
            : null;

        if (existingPackages is not null)
        {
            var shippedPackages = await JsonSerializer.DeserializeAsync<ClusterDependency[]?>(stream, DefaultJsonSerializerSettings.Default, stoppingToken)
                ?? throw new InvalidOperationException("Failed to deserialize internal package set");

            var operations = new List<ClusterDependencyOperation>();

            foreach (var package in existingPackages)
            {
                var shippedPackage = shippedPackages.FirstOrDefault(p => p.Name == package.Name);
                if (shippedPackage is null)
                    continue; // manually added package

                if (!SemVersion.TryParse(shippedPackage.Version, out var shippedVersion))
                {
                    logger.LogShippedParseVersionFailure(shippedPackage.Version, shippedPackage.Name);
                    continue;
                }

                if (!SemVersion.TryParse(package.Version, out var packageVersion))
                {
                    logger.LogParseVersionFailure(package.Version, package.Name);
                    continue;
                }

                // Shipped versions is lower than configured one
                if (SemVersion.CompareSortOrder(shippedVersion, packageVersion) <= 0)
                    continue;

                // add an operation to the update list
                operations.Add(new ClusterDependencyOperation(shippedPackage, ClusterDependencyOperationKind.Install));
            }

            // enqueue package updates if there are any shipped packages with higher version than the installed ones
            await EnqueueOperations(operations, stoppingToken);
        }
        else
        {
            await atomicFileWriter.WriteAsync(destination, destinationStream => stream.CopyToAsync(destinationStream, stoppingToken), stoppingToken);

            logger.LogInitializationFromEmbeddedResources();
        }
    }

    /// <summary>
    /// Reads the packages file and returns <see langword="null"/> if its content is corrupted or incomplete.
    /// </summary>
    private async Task<IReadOnlyCollection<ClusterDependency>?> TryLoadDependencies(string packagesFilename, CancellationToken cancellationToken)
    {
        try
        {
            return await DeserializePackagesFile(packagesFilename, DefaultJsonSerializerSettings.Default, cancellationToken);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            logger.LogCorruptedPackagesFile(e, packagesFilename);
            return null;
        }
    }

    /// <summary>
    /// Read the cluster dependencies from the packages file in the home folder.
    /// </summary>
    public async Task<IReadOnlyCollection<ClusterDependency>> LoadDependencies(CancellationToken cancellationToken)
    {
        var packagesFilename = GetPackagesFilepath(workspaceProvider.Home, options.Value.PackageSet, fileSystem);
        return await DeserializePackagesFile(packagesFilename, DefaultJsonSerializerSettings.Default, cancellationToken);
    }

    /// <summary>
    /// Enqueues the specified cluster dependency operations to be applied on next restart. Multiple calls will modify
    /// the list of queued operations, overwriting already queued packages with new versions or remove them on uninstall.
    /// </summary>    
    public async Task<IReadOnlyList<ClusterDependencyChange>> EnqueueOperations(List<ClusterDependencyOperation> operations, CancellationToken cancellationToken)
    {
        if (operations.Count == 0)
        {
            logger.LogNothingToQueue();
            return [];
        }

        // Keep previous updates if no restart was performed meanwhile
        var previousUpdates = await DeserializeQueuedUpdates(cancellationToken);
        var currentDependencies = await LoadDependencies(cancellationToken);
        var changes = new List<ClusterDependencyChange>();

        // Overwrite already queued packages with new versions
        var updatedPackages = operations
                .UnionBy(previousUpdates, cp => cp.Dependency.Name)
                .Where(updatedPackage =>
                {
                    // It might happen that a package gets uninstalled and afterwards installed again with
                    // same version that is already in the packages file. In this case we can remove the operation
                    var currentDependency = currentDependencies.FirstOrDefault(cd => cd.Name == updatedPackage.Dependency.Name);
                    if (updatedPackage.OperationKind == ClusterDependencyOperationKind.Uninstall && currentDependency is not null
                        && currentDependency.Version != updatedPackage.Dependency.Version)
                    {
                        logger.LogRemovingUninstallOperation(updatedPackage.Dependency.Name, updatedPackage.Dependency.Version);

                        changes.Add(new ClusterDependencyChange(CrudAction.Deleted, updatedPackage));
                        return false;
                    }

                    if (updatedPackage.OperationKind == ClusterDependencyOperationKind.Install && currentDependency?.Version == updatedPackage.Dependency.Version)
                    {
                        logger.LogRemovingInstallOperation(updatedPackage.Dependency.Name, updatedPackage.Dependency.Version);

                        changes.Add(new ClusterDependencyChange(CrudAction.Deleted, updatedPackage));
                        return false;
                    }

                    return true;
                })
                .OrderBy(p => p.Dependency.Name)
                .ToList();

        var updateFile = GetUpdateFilePath();
        changes.AddRange(GetClusterDependencyChanges(previousUpdates, updatedPackages));

        if (updatedPackages.Count == 0)
        {
            logger.LogOperationsFileDeletion(updateFile);
            fileSystem.File.Delete(updateFile);
            return changes;
        }

        logger.LogFileEntriesCount(updatedPackages.Count, updateFile);

        // Rewrite file containing queued updates
        await atomicFileWriter.WriteAsync(updateFile,
            stream => JsonSerializer.SerializeAsync(stream, updatedPackages, DefaultJsonSerializerSettings.Default, cancellationToken),
            cancellationToken);
        return changes;
    }

    private static IEnumerable<ClusterDependencyChange> GetClusterDependencyChanges(List<ClusterDependencyOperation> previous, List<ClusterDependencyOperation> next)
    {
        var result = new List<ClusterDependencyChange>();

        foreach (var prev in previous)
        {
            var nextOp = next.FirstOrDefault(n => n.Dependency.Name == prev.Dependency.Name);
            if (nextOp is null)
            {
                result.Add(new ClusterDependencyChange(CrudAction.Deleted, prev));
                continue;
            }

            if (nextOp.OperationKind != prev.OperationKind || nextOp.Dependency.Version != prev.Dependency.Version)
            {
                result.Add(new ClusterDependencyChange(CrudAction.Updated, nextOp));
            }
        }

        var added = next.Where(n => !previous.Any(p => p.Dependency.Name == n.Dependency.Name))
            .Select(n => new ClusterDependencyChange(CrudAction.Created, n));

        return result.Concat(added);
    }

    /// <summary>
    /// Updates the cluster dependencies in home folder with the queued package updates.
    /// </summary>    
    public async Task ProcessQueuedOperations(CancellationToken cancellationToken)
    {
        var packageUpdates = await DeserializeQueuedUpdates(cancellationToken);
        if (packageUpdates.Count == 0)
        {
            logger.LogNoPackagesQueued();
            return;
        }

        var packagesFilename = GetPackagesFilepath(workspaceProvider.Home, options.Value.PackageSet, fileSystem);
        var existingPackages = await DeserializePackagesFile(packagesFilename, DefaultJsonSerializerSettings.Default, cancellationToken);

        // Get the packages to be removed
        var packagesToRemove = packageUpdates
            .Where(k => k.OperationKind == ClusterDependencyOperationKind.Uninstall)
            .Select(k => k.Dependency.Name)
            .ToHashSet();

        // Overwrite already queued packages with new versions
        var updatedPackages = packageUpdates
            .Where(k => k.OperationKind == ClusterDependencyOperationKind.Install)
            .Select(k => k.Dependency)
            .UnionBy(existingPackages, cp => cp.Name)
            .Where(p => !packagesToRemove.Contains(p.Name))
            .OrderBy(p => p.Name)
            .ToList();

        logger.LogWriteCount(updatedPackages.Count, packagesFilename);

        // Write updated packages file
        await atomicFileWriter.WriteAsync(packagesFilename,
            stream => JsonSerializer.SerializeAsync(stream, updatedPackages, DefaultJsonSerializerSettings.Default, cancellationToken),
            cancellationToken);

        var filePath = GetUpdateFilePath();
        logger.LogUpdateFileDeletion(filePath);
        fileSystem.File.Delete(filePath);
    }

    private async Task<List<ClusterDependencyOperation>> DeserializeQueuedUpdates(CancellationToken cancellationToken)
    {
        var updateFile = GetUpdateFilePath();
        if (!fileSystem.File.Exists(updateFile))
            return [];

        try
        {
            using var updateFileStream = fileSystem.FileStream.New(updateFile, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous,
            });
            var updates = await JsonSerializer.DeserializeAsync<List<ClusterDependencyOperation>>(updateFileStream, DefaultJsonSerializerSettings.Default, cancellationToken);
            if (updates is null)
                logger.LogDeserializationFailure(updateFile);

            return updates ?? [];
        }
        catch (JsonException e)
        {
            // A partially written operations file must not brick startup, the queued operations get discarded.
            logger.LogCorruptedOperationsFile(e, updateFile);
            fileSystem.File.Delete(updateFile);
            return [];
        }
    }

    internal async Task<ClusterDependency[]> DeserializePackagesFile(string filename, JsonSerializerOptions serializerOptions, CancellationToken cancellationToken)
    {
        await using var fileStream = fileSystem.FileStream.New(filename, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous,
        });
        return await JsonSerializer.DeserializeAsync<ClusterDependency[]>(fileStream, serializerOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to deserialize {PackagesFileName} file '{filename}'.");
    }

    public async Task<IReadOnlyCollection<ClusterDependencyOperation>> GetEnqueuedOperations(CancellationToken cancellationToken)
        => await DeserializeQueuedUpdates(cancellationToken);


    public Dictionary<string, IEnumerable<string>> BuildStoredPackageVersionMap()
    {
        var dependenciesFolder = fileSystem.GetDependenciesPath(workspaceProvider);
        if (!fileSystem.Directory.Exists(dependenciesFolder))
            throw new DirectoryNotFoundException($"Dependencies folder '{dependenciesFolder}' does not exist.");

        var result = new Dictionary<string, IEnumerable<string>>();

        // dependencies/<package_name>/<version>/..
        foreach (var packageDirectory in fileSystem.Directory.EnumerateDirectories(dependenciesFolder))
        {
            // our package key from folder
            var packageName = Path.GetFileName(packageDirectory);
            if (string.IsNullOrEmpty(packageName))
                continue;

            // our package versions
            var versions = fileSystem.Directory.EnumerateDirectories(packageDirectory)
                .Select(fileSystem.Path.GetFileName)
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(v => v!)
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            // no versions found for this package
            if (versions.Count == 0)
                continue;

            result[packageName] = versions;
        }

        return result;
    }
}
