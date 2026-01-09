using System.Text.Json;
using ClusterManagement.Backend.Contracts;
using Sdk.Backend.Artifacts;

namespace ClusterManagement.Backend.Extensions;

internal static class IArtifactRepositoryExtensions
{
    private static async Task<ArtifactMetadata> DownloadMetadata(this IArtifactRepository repository, IArtifact metadataArtifact, CancellationToken cancellationToken = default)
    {
        await using var metadataStream = await repository.Download(metadataArtifact, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to download metadata for name='{metadataArtifact.Name}'.");

        return await JsonSerializer.DeserializeAsync<ArtifactMetadata?>(metadataStream, JsonSerializerOptions.Default, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to deserialize metadata for name='{metadataArtifact.Name}'.");
    }

    /// <summary>
    /// Query zip artifact and deserialized metadata for given metadata (json) artifact.
    /// </summary>
    /// <param name="metadataArtifact">e.g. artifact item like 0.28.0-linux-x64_0.48.0.json</param>
    /// <param name="cancellationToken">token to cancel async operations</param>    
    /// <exception cref="ArgumentException">thrown if metadataArtifact.Name can't be parsed</exception>
    /// <exception cref="InvalidOperationException">thrown if metadataArtifact content can't be downloaded</exception>
    public static async Task<ZipArtifact> QueryZipArtifactWithMetadata(this IArtifactRepository repository, IArtifact metadataArtifact, CancellationToken cancellationToken = default)
    {
        // e.g. 0.28.0-linux-x64_0.48.0.json -> {packageVersion}-{runtime}_{coreVersion}
        if (!MetadataArtifactNameRegex.Parse(metadataArtifact.Name, out var packageVersion, out var runtime, out _))
            throw new ArgumentException($"Failed to parse metadata name='{metadataArtifact.Name}' in path='{metadataArtifact.Path}'.");

        // we download the metadata but we could spare the call until metadata contains more information
        // than we can extract from it's name
        var metadata = await repository.DownloadMetadata(metadataArtifact, cancellationToken);

        // e.g. 0.28.0-linux-x64.zip
        var zipArtifactName = $"{packageVersion}-{runtime}.zip";
        var queryBuilder = repository.CreateQueryBuilder();
        var query = queryBuilder
            .AndPathMatches(metadataArtifact.Path)
            .AndNameMatches(zipArtifactName)
            .Build();

        // we could also create the engine host artifact item from json artifact to safe a call
        var zipArtifacts = await repository.Query(query, cancellationToken);
        var zipArtifact = zipArtifacts.Artifacts.Single();

        return new ZipArtifact
        {
            Name = zipArtifact.Name,
            Path = zipArtifact.Path,
            Modified = zipArtifact.Modified,
            Size = zipArtifact.Size,
            Metadata = metadata
        };
    }
}
