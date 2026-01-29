using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Reflection;
using System.Text.Json;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Internal;
using ClusterManagement.Internal.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Backend.Modules;
using Sdk.Messaging;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Designs;

internal sealed class ClusterDependencyStore(
    IFileSystem fileSystem,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IOptions<ClusterManagementOptions> options,
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
    /// If <see cref="ClusterManagementOptions.OverwritePackagesFiles"/> is set to true or the packages file does not exist,
    /// the initial packages file is copied from the embedded resources to the workspace.
    /// </summary>
    public async Task Initialize(CancellationToken stoppingToken)
    {
        var destination = GetPackagesFilepath(workspaceProvider.Home, options.Value.PackageSet, fileSystem);
        if (fileSystem.File.Exists(destination) && !options.Value.OverwritePackagesFiles)
            return;

        var name = string.Join('.', typeof(ClusterManagementBackendModule).Namespace, PackagesRessourceFolder, GetPackagesFilename(options.Value.PackageSet));
        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream is null)
            return;

        logger.LogInformation("Use cluster packages from embedded resources");

        await using var destinationStream = fileSystem.FileStream.New(destination, FileMode.Create);
        await stream.CopyToAsync(destinationStream, stoppingToken);
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
    public async Task EnqueueOperations(List<ClusterDependencyOperation> operations, CancellationToken cancellationToken)
    {
        if (operations.Count == 0)
        {
            logger.LogInformation("No package updates to queue");
            return;
        }

        // Keep previous updates if no restart was performed meanwhile
        var previousUpdates = await DeserializeQueuedUpdates(cancellationToken);
        var currentDependencies = await LoadDependencies(cancellationToken);

        // Overwrite already queued packages with new versions
        var updatedPackages = operations
                .UnionBy(previousUpdates, cp => cp.Dependency.Name)
                .Where(updatedPackages =>
                {
                    // It might happen that a package gets uninstalled and afterwards installed again with
                    // same version that is already in the packages file. In this case we can remove the operation
                    var currentDependency = currentDependencies.FirstOrDefault(cd => cd.Name == updatedPackages.Dependency.Name);
                    if (updatedPackages.Operation == ClusterDependencyOperationKind.Uninstall && currentDependency is not null
                        && currentDependency.Version != updatedPackages.Dependency.Version)
                    {
                        logger.LogDebug("Removing uninstall operation from queue because '{PackageName}' version '{PackageVersion}' it's not installed yet",
                            updatedPackages.Dependency.Name, updatedPackages.Dependency.Version);

                        return false;
                    }

                    if (updatedPackages.Operation == ClusterDependencyOperationKind.Install && currentDependency?.Version == updatedPackages.Dependency.Version)
                    {
                        logger.LogDebug("Removing install operation from queue because '{PackageName}' version '{PackageVersion}' is already installed",
                            updatedPackages.Dependency.Name, updatedPackages.Dependency.Version);
                        return false;
                    }

                    return true;
                })
                .OrderBy(p => p.Dependency.Name)
                .ToList();

        var updateFile = GetUpdateFilePath();

        if (updatedPackages.Count == 0)
        {
            logger.LogInformation("No package operations to queue, deleting existing operations file '{UpdateFile}'", updateFile);
            fileSystem.File.Delete(updateFile);
            return;
        }

        logger.LogInformation("Enqueued {Count} package operations to '{UpdateFile}'", updatedPackages.Count, updateFile);

        // Rewrite file containing queued updates
        using var fileStream = fileSystem.FileStream.New(updateFile, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(fileStream, updatedPackages, DefaultJsonSerializerSettings.Default, cancellationToken);
    }

    /// <summary>
    /// Updates the cluster dependencies in home folder with the queued package updates.
    /// </summary>    
    public async Task ProcessQueuedOperations(CancellationToken cancellationToken)
    {
        var packageUpdates = await DeserializeQueuedUpdates(cancellationToken);
        if (packageUpdates.Count == 0)
        {
            logger.LogDebug("No queued package operations found");
            return;
        }

        var packagesFilename = GetPackagesFilepath(workspaceProvider.Home, options.Value.PackageSet, fileSystem);
        var existingPackages = await DeserializePackagesFile(packagesFilename, DefaultJsonSerializerSettings.Default, cancellationToken);

        // Get the packages to be removed
        var packagesToRemove = packageUpdates
            .Where(k => k.Operation == ClusterDependencyOperationKind.Uninstall)
            .Select(k => k.Dependency.Name)
            .ToHashSet();

        // Overwrite already queued packages with new versions
        var updatedPackages = packageUpdates
            .Where(k => k.Operation == ClusterDependencyOperationKind.Install)
            .Select(k => k.Dependency)
            .UnionBy(existingPackages, cp => cp.Name)
            .Where(p => !packagesToRemove.Contains(p.Name))
            .OrderBy(p => p.Name)
            .ToList();

        logger.LogDebug("Write {Count} packages to '{PackageFile}'", updatedPackages.Count, packagesFilename);

        // Write updated packages file
        using var fileStream = fileSystem.FileStream.New(packagesFilename, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(fileStream, updatedPackages, DefaultJsonSerializerSettings.Default, cancellationToken);

        logger.LogDebug("Deleting queued package updates file '{UpdateFile}'", GetUpdateFilePath());
        fileSystem.File.Delete(GetUpdateFilePath());
    }

    private async Task<List<ClusterDependencyOperation>> DeserializeQueuedUpdates(CancellationToken cancellationToken)
    {
        var updateFile = GetUpdateFilePath();
        if (!fileSystem.File.Exists(updateFile))
            return [];

        using var updateFileStream = fileSystem.FileStream.New(updateFile, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous,
        });
        var updates = await JsonSerializer.DeserializeAsync<List<ClusterDependencyOperation>>(updateFileStream, DefaultJsonSerializerSettings.Default, cancellationToken);
        if (updates is null)
            logger.LogWarning("Failed to deserialize queued package updates from '{UpdateFile}'", updateFile);

        return updates ?? [];
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


    public Dictionary<string, IEnumerable<Version>> BuildStoredPackageVersionMap()
    {
        var dependenciesFolder = fileSystem.GetDependenciesPath(workspaceProvider);
        if (!fileSystem.Directory.Exists(dependenciesFolder))
            throw new DirectoryNotFoundException($"Dependencies folder '{dependenciesFolder}' does not exist.");

        var result = new Dictionary<string, IEnumerable<Version>>();

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
                .Distinct()
                .Select(v => Version.Parse(v!))
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
