using System.IO.Abstractions;
using System.Runtime.InteropServices;
using ClusterManagement.Backend.Contracts;
using ClusterManagement.Backend.EngineHosts.Extensions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Internal;
using Microsoft.Extensions.Options;
using Sdk.Backend.Artifacts;
using Sdk.Backend.Modules;
using Semver;

namespace ClusterManagement.Backend.EngineHosts;

internal sealed class EngineHostArtifactRepository(
    IFileSystem fileSystem,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IArtifactRepository repository,
    IOptions<ClusterManagementOptions> options) : IEngineHostArtifactRepository
{
    private const string EngineHostFolder = "enginehost";
    private readonly ClusterManagementOptions _options = options.Value;

    /// <inheritdoc/>
    public Task<HttpLoaderResult> DownloadAndExtract(CancellationToken cancellationToken)
    {
        var packageName = _options.GetEngineHostPackageName();
        var engineHostVersionPath = fileSystem.GetEngineHostVersionsPath(workspaceProvider);
        var targetDirectory = fileSystem.Path.Combine(engineHostVersionPath, packageName);

        return ProcessDownload(packageName, targetDirectory,
            () => fileSystem.Directory.Exists(targetDirectory) && fileSystem.Directory.EnumerateFileSystemEntries(targetDirectory).Any(), cancellationToken);
    }

    public async Task<HttpLoaderResult> ProcessDownload(string packageName, string targetPath, Func<bool>? skipTarget, CancellationToken cancellationToken)
    {
        var result = new HttpLoaderResult($"{EngineHostFolder}/{packageName}", targetPath);

        try
        {
            if (skipTarget is not null && skipTarget())
            {
                result.Skipped = true;
                return result;
            }

            var sourceArtifact = await QueryArtifact(packageName, cancellationToken)
                ?? throw new InvalidOperationException($"Artifact '{packageName}' not found in repository.");

            await repository.DownloadAndExtract(sourceArtifact, targetPath, cancellationToken);
        }
        catch (Exception e)
        {
            result.Error = e;
        }

        return result;
    }

    private async Task<IArtifact?> QueryArtifact(string packageName, CancellationToken cancellationToken)
    {
        var queryBuilder = repository.CreateQueryBuilder();
        var query = queryBuilder
                .AndPathMatches(EngineHostFolder)
                .AndNameMatches($"{packageName}.zip")
                .Build();

        var artifacts = await repository.Query(query, cancellationToken);
        return artifacts.Artifacts.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IArtifact>> QueryMetadataArtifacts(SemVersion? coreVersion, CancellationToken cancellationToken = default)
    {
        // artifact items e.g. 0.28.0-linux-x64_0.48.0.json
        var versionFilter = coreVersion is not null ? $"{coreVersion.Major}.{coreVersion.Minor}." : string.Empty;
        var queryBuilder = repository.CreateQueryBuilder();
        var query = queryBuilder
                .AndPathMatches(EngineHostFolder)
                .AndNameMatches($"*-{RuntimeInformation.RuntimeIdentifier}_{versionFilter}*.json")
                .OrderByDescending("updated")
                .Build();

        var artifactResult = await repository.Query(query, cancellationToken);
        return artifactResult.Artifacts;
    }

    /// <inheritdoc/>
    public async Task<ZipArtifact?> QueryLatestZipArtifact(SemVersion coreVersion, CancellationToken cancellationToken = default)
    {
        var metadataArtifacts = await QueryMetadataArtifacts(coreVersion, cancellationToken);
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
