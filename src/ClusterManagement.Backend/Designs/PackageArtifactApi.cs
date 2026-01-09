using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text.Json;
using ClusterManagement.Backend.Contracts;
using ClusterManagement.Backend.Designs.Extensions;
using ClusterManagement.Backend.Extensions;
using Sdk.Backend.ArtifactApi;
using Sdk.Backend.Modules;
using Sdk.Messaging;
using Semver;
using ViciOne.Cluster.Model;

namespace ClusterManagement.Backend.Designs;

internal sealed class PackageArtifactApi(
    IFileSystem fileSystem,
    IWorkspaceProvider<ClusterManagementBackendModule> workspaceProvider,
    IArtifactQueryApi api) : IPackageArtifactApi
{
    private const string FunctionBlockFolder = "fbs";

    private IArtifactItem CreateArtifactItem(string functionBlockName, Version version, string? runtimeIdentifier = default)
    {
        var fileNameWithoutExtension = runtimeIdentifier is null ? version.ToString() : $"{version}-{runtimeIdentifier}";
        var path = $"{FunctionBlockFolder}/{functionBlockName}";

        return api.CreateArtifactItem(path, $"{fileNameWithoutExtension}.zip");
    }

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
    public Task<HttpLoaderResult> DownloadAndExtract(string functionBlockName, Version version, CancellationToken cancellationToken)
    {
        var dependenciesPath = fileSystem.GetDependenciesPath(workspaceProvider);
        var targetPath = fileSystem.Path.Combine(dependenciesPath, functionBlockName, version.ToString());

        var artifact = CreateArtifactItem(functionBlockName, version, RuntimeInformation.RuntimeIdentifier);
        return api.ProcessDownload(artifact, targetPath, () => FunctionBlockVersionExists(fileSystem, targetPath), cancellationToken);

        static bool FunctionBlockVersionExists(IFileSystem fs, string functionBlockPath)
            => fs.Directory.Exists(functionBlockPath) && fs.Directory.GetFiles(functionBlockPath).Length > 0;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IArtifactItem>> QueryMetadataArtifacts(SemVersion? coreVersion, string? packageName = null, CancellationToken cancellationToken = default)
    {
        // artifact items e.g. name=1.18.0-linux-x64_0.48.0.json, path=fbs/ViciOne.Driver.IoTCore
        // * because they are in subfolders (fb name)
        var folderFilter = string.IsNullOrEmpty(packageName) ? "*" : packageName;
        var versionFilter = coreVersion is not null ? $"{coreVersion.Major}.{coreVersion.Minor}." : string.Empty;
        var queryBuilder = api.CreateQueryBuilder();
        var query = queryBuilder
                .AndPathMatches($"{FunctionBlockFolder}/{folderFilter}")
                .AndNameMatches($"*-{RuntimeInformation.RuntimeIdentifier}_{versionFilter}*.json")
                .OrderByDescending("updated")
                .BuildQueryString();

        var artifactResult = await api.ExecuteQuery(query, cancellationToken);
        return artifactResult.Artifacts;
    }

    /// <inheritdoc/>
    public async Task<ZipArtifactItem?> QueryLatestZipArtifactItem(SemVersion coreVersion, string packageName, CancellationToken cancellationToken = default)
    {
        var metadataArtifacts = await QueryMetadataArtifacts(coreVersion, packageName, cancellationToken);
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
