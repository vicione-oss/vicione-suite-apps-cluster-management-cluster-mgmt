using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text.Json;
using ClusterManagement.Backend.Contracts;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Backend.Extensions;
using Sdk.Backend.Artifacts;
using Sdk.Backend.Modules;
using Sdk.Messaging;
using Semver;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Designs;

internal sealed class PackageArtifactRepository(
    IFileSystem fileSystem,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IArtifactRepository repository) : IPackageArtifactRepository
{
    private const string FunctionBlockFolder = "fbs";

    internal static async Task<ClusterDependency[]> DeserializeClusterDependencies(IFileSystem fileSystem, string dependencyFile, CancellationToken cancellationToken)
    {
        await using var fs = fileSystem.FileStream.New(dependencyFile, FileMode.Open, FileAccess.Read);
        var dependencies = await JsonSerializer.DeserializeAsync<ClusterDependency[]>(fs, DefaultJsonSerializerSettings.Default, cancellationToken);

        return dependencies ?? throw new InvalidOperationException($"Failed to deserialize dependency json file {dependencyFile}");
    }

    /// <inheritdoc/>
    public async Task<HttpLoaderResult[]> DownloadAndExtract(string dependencyFile, CancellationToken cancellationToken)
    {
        var dependencies = await DeserializeClusterDependencies(fileSystem, dependencyFile, cancellationToken);

        return await Task.WhenAll(dependencies.Select(fb => DownloadAndExtract(fb.Name, fb.Version, cancellationToken)));
    }

    /// <inheritdoc/>
    public Task<HttpLoaderResult> DownloadAndExtract(string functionBlockName, string version, CancellationToken cancellationToken)
    {
        var dependenciesPath = fileSystem.GetDependenciesPath(workspaceProvider);
        var targetPath = fileSystem.Path.Combine(dependenciesPath, functionBlockName, version.ToString());

        return ProcessDownload(functionBlockName, version, targetPath, () => FunctionBlockVersionExists(fileSystem, targetPath), cancellationToken);

        static bool FunctionBlockVersionExists(IFileSystem fs, string functionBlockPath)
            => fs.Directory.Exists(functionBlockPath) && fs.Directory.GetFiles(functionBlockPath).Length > 0;
    }

    public async Task<HttpLoaderResult> ProcessDownload(string functionBlockName, string version, string targetPath, Func<bool>? skipTarget, CancellationToken cancellationToken)
    {
        var result = new HttpLoaderResult($"{FunctionBlockFolder}/{functionBlockName}/{version}", targetPath);

        try
        {
            if (skipTarget is not null && skipTarget())
            {
                result.Skipped = true;
                return result;
            }

            var sourceArtifact = await QueryArtifact(functionBlockName, version, cancellationToken)
                ?? throw new InvalidOperationException($"Artifact '{functionBlockName}' version '{version}' not found in repository.");

            await repository.DownloadAndExtract(sourceArtifact, targetPath, cancellationToken);
        }
        catch (Exception e)
        {
            result.Error = e;
        }

        return result;
    }

    private async Task<IArtifact?> QueryArtifact(string functionBlockName, string version, CancellationToken cancellationToken)
    {
        var fileNameWithoutExtension = $"{version}-{RuntimeInformation.RuntimeIdentifier}";
        var queryBuilder = repository.CreateQueryBuilder();
        var query = queryBuilder
                .AndPathMatches($"{FunctionBlockFolder}/{functionBlockName}")
                .AndNameMatches($"{fileNameWithoutExtension}.zip")
                .Build();

        var artifacts = await repository.Query(query, cancellationToken);
        return artifacts.Artifacts.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IArtifact>> QueryMetadataArtifacts(SemVersion? coreVersion, string? packageName = null, CancellationToken cancellationToken = default)
    {
        // artifact items e.g. name=1.18.0-linux-x64_0.48.0.json, path=fbs/ViciOne.Driver.IoTCore
        // * because they are in subfolders (fb name)
        var folderFilter = string.IsNullOrEmpty(packageName) ? "*" : packageName;
        var versionFilter = coreVersion is not null ? $"{coreVersion.Major}.{coreVersion.Minor}." : string.Empty;
        var queryBuilder = repository.CreateQueryBuilder();
        var query = queryBuilder
                .AndPathMatches($"{FunctionBlockFolder}/{folderFilter}")
                .AndNameMatches($"*-{RuntimeInformation.RuntimeIdentifier}_{versionFilter}*.json")
                .OrderByDescending("updated")
                .Build();

        var artifactResult = await repository.Query(query, cancellationToken);
        return artifactResult.Artifacts;
    }

    /// <inheritdoc/>
    public async Task<ZipArtifact?> QueryLatestZipArtifact(SemVersion coreVersion, string packageName, CancellationToken cancellationToken = default)
    {
        var metadataArtifacts = await QueryMetadataArtifacts(coreVersion, packageName, cancellationToken);
        var latestArtifact = metadataArtifacts.FirstOrDefault();
        if (latestArtifact is null)
            return null;

        return await QueryZipArtifact(latestArtifact, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">thrown if metadataArtifact.Name can't be parsed</exception>
    /// <exception cref="InvalidOperationException">thrown if metadataArtifact content can't be downloaded</exception>
    public Task<ZipArtifact> QueryZipArtifact(IArtifact metadataArtifact, CancellationToken cancellationToken = default)
        => repository.QueryZipArtifactWithMetadata(metadataArtifact, cancellationToken);
}
