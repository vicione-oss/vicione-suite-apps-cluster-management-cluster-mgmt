using System.IO.Abstractions;
using System.Runtime.InteropServices;
using ClusterManagement.Backend.Contracts;
using ClusterManagement.Backend.EngineHosts.Extensions;
using ClusterManagement.Backend.Extensions;
using ClusterManagement.Internal;
using Microsoft.Extensions.Options;
using Sdk.Backend.ArtifactApi;
using Sdk.Backend.Modules;
using Semver;

namespace ClusterManagement.Backend.EngineHosts;

internal sealed class EngineHostArtifactApi(
    IFileSystem fileSystem,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IArtifactQueryApi api,
    IOptions<ClusterManagementOptions> options) : IEngineHostArtifactApi
{
    private const string EngineHostFolder = "enginehost";
    private readonly ClusterManagementOptions _options = options.Value;


    /// <inheritdoc/>
    public async Task<HttpLoaderResult> DownloadAndExtract(CancellationToken cancellationToken)
    {
        var packageName = _options.GetEngineHostPackageName();
        var sourceArtifact = api.CreateArtifactItem(EngineHostFolder, $"{packageName}.zip");
        var engineHostVersionPath = fileSystem.GetEngineHostVersionsPath(workspaceProvider);
        var targetDirectory = fileSystem.Path.Combine(engineHostVersionPath, packageName);

        return await api.ProcessDownload(sourceArtifact, targetDirectory,
            () => fileSystem.Directory.Exists(targetDirectory) && fileSystem.Directory.EnumerateFileSystemEntries(targetDirectory).Any(), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IArtifactItem>> QueryMetadataArtifacts(SemVersion? coreVersion, CancellationToken cancellationToken = default)
    {
        // artifact items e.g. 0.28.0-linux-x64_0.48.0.json
        var versionFilter = coreVersion is not null ? $"{coreVersion.Major}.{coreVersion.Minor}." : string.Empty;
        var queryBuilder = api.CreateQueryBuilder();
        var query = queryBuilder
                .AndPathMatches(EngineHostFolder)
                .AndNameMatches($"*-{RuntimeInformation.RuntimeIdentifier}_{versionFilter}*.json")
                .OrderByDescending("updated")
                .BuildQueryString();

        var artifactResult = await api.ExecuteQuery(query, cancellationToken);
        return artifactResult.Artifacts;
    }

    /// <inheritdoc/>
    public async Task<ZipArtifactItem?> QueryLatestZipArtifactItem(SemVersion coreVersion, CancellationToken cancellationToken = default)
    {
        var metadataArtifacts = await QueryMetadataArtifacts(coreVersion, cancellationToken);
        var latestArtifact = metadataArtifacts.FirstOrDefault();
        if (latestArtifact is null)
            return null;

        return await QueryZipArtifactItem(latestArtifact, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">thrown if metadataArtifact.Name can't be parsed</exception>
    /// <exception cref="InvalidOperationException">thrown if metadataArtifact content can't be downloaded</exception>
    public Task<ZipArtifactItem> QueryZipArtifactItem(IArtifactItem metadataArtifact, CancellationToken cancellationToken = default)
        => api.QueryZipArtifactItemWithMetadata(metadataArtifact, cancellationToken);
}
