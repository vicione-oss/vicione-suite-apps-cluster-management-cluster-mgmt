using ClusterManagement.Backend.Contracts;
using Sdk.Backend.Artifacts;
using Semver;

namespace ClusterManagement.Backend.EngineHosts;

/// <summary>
/// Provides methods for downloading and querying EngineHost package artifacts.
/// </summary>
public interface IEngineHostArtifactRepository
{
    /// <summary>
    /// Downloads the EngineHost ZIP package specified by 
    /// <see cref="Internal.ClusterManagementOptions.EngineHostVersion"/> 
    /// and extracts it to the local file system.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that returns an <see cref="HttpLoaderResult"/> representing the result of the download and extraction operation.
    /// </returns>
    Task<HttpLoaderResult> DownloadAndExtract(CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the latest EngineHost ZIP package artifact that supports the specified core version.
    /// </summary>
    /// <param name="coreVersion">The required core version (e.g., 0.48.0).</param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that returns the latest <see cref="ZipArtifact"/> that supports the specified <paramref name="coreVersion"/>, 
    /// or <see langword="null"/> if no compatible package is found.
    /// </returns>
    Task<ZipArtifact?> QueryLatestZipArtifact(SemVersion coreVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries all EngineHost metadata artifacts (JSON), or filters them to only those supporting a specified core version.
    /// </summary>
    /// <param name="coreVersion">
    /// The core version to filter by, or <see langword="null"/> to return all available metadata artifacts.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that returns a collection of <see cref="IArtifact"/> representing the matching metadata artifacts.
    /// </returns>
    Task<IEnumerable<IArtifact>> QueryMetadataArtifacts(SemVersion? coreVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the EngineHost ZIP package artifact that corresponds to the specified metadata artifact.
    /// </summary>
    /// <param name="metadataArtifact">
    /// The metadata artifact to resolve, such as an item named <c>0.28.0-linux-x64_0.48.0.json</c>.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task that returns the corresponding <see cref="ZipArtifact"/>, enriched with metadata from the given artifact.
    /// </returns>
    Task<ZipArtifact> QueryZipArtifact(IArtifact metadataArtifact, CancellationToken cancellationToken = default);
}
